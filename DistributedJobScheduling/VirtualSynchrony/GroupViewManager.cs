using System.Threading;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Topics;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.DependencyInjection;
using System.Linq;
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.LeaderElection.KeepAlive;
using DistributedJobScheduling.LeaderElection;
using DistributedJobScheduling.Queues;

namespace DistributedJobScheduling.VirtualSynchrony
{

    //TODO: Timeouts?
    //TODO: Re-Broadcast messages? This is part of the original algorithm but can we get away with dropping them like we are doing?
    //FIXME: Resource Locks? Messages are asynchronous, some blocks should have mutual exclusion
    public class GroupViewManager : IGroupViewManager, IStartable
    {
        public int JoinRequestTimeout { get; set; } = 5000;
        private class MulticastNotDeliveredException : Exception {}
        private class NotDeliveredException : Exception {}
        private class KickedFromViewException : Exception {
            public KickedFromViewException() : base("Consolidated view without this node") {}
        }

        public Group View { get; private set; }
        public event Action ViewChanging;

        public ITopicOutlet Topics { get; private set; }

        private ICommunicationManager _communicationManager;
        private ITimeStamper _messageTimeStamper;
        private ILogger _logger;
        private Node.INodeRegistry _nodeRegistry;
        private ITopicPublisher _virtualSynchronyTopic;

        private CancellationTokenSource _senderCancellationTokenSource;
        private Task _messageSender;
        private AsyncGenericQueue<(Node, Message)> _sendQueue;
        private Dictionary<Message, TaskCompletionSource<bool>> _messageSendStateMap;
        private Queue<(Node, Message)> _onHoldMessages;

        
        #region Messaging Variables

        /// <summary>
        /// Maps the tuple (senderID, timestamp) to the hashset of nodes that need to acknowledge the message
        /// (Receiving the message is a type of aknowledgment, acks might arrive before the message)
        /// </summary>
        private Dictionary<(int,int), HashSet<Node>> _confirmationMap;
        private Dictionary<(int,int), TemporaryMessage> _confirmationQueue;
        private HashSet<TemporaryMessage> _sentTemporaryMessages;
        private Dictionary<TemporaryMessage, TaskCompletionSource<bool>> _sendComplenentionMap;

        #endregion

        #region View Change Variables

        private CancellationTokenSource _joinRequestCancellation;
        private ViewJoinRequest _currentJoinRequest;
        private ViewChangeMessage.ViewChange _pendingViewChange;
        private TaskCompletionSource<bool> _viewChangeInProgress;
        private HashSet<Node> _newGroupView;
        private HashSet<Node> _flushedNodes;
        private bool _flushed;

        #endregion

        public GroupViewManager() : this(DependencyManager.Get<Node.INodeRegistry>(),
                                         DependencyManager.Get<ICommunicationManager>(), 
                                         DependencyManager.Get<ITimeStamper>(),
                                         DependencyManager.Get<IConfigurationService>(),
                                         DependencyManager.Get<ILogger>()
                                         ) {}
        internal GroupViewManager(Node.INodeRegistry nodeRegistry,
                                  ICommunicationManager communicationManager, 
                                  ITimeStamper timeStamper,
                                  IConfigurationService configurationService,
                                  ILogger logger,
                                  int joinRequestTimeout = 5000,
                                  Group coldStartView = null)
        {
            _nodeRegistry = nodeRegistry;
            _communicationManager = communicationManager;
            _messageTimeStamper = timeStamper;
            _logger = logger;
            _confirmationMap = new Dictionary<(int, int), HashSet<Node>>();
            _confirmationQueue = new Dictionary<(int, int), TemporaryMessage>();
            _sentTemporaryMessages = new HashSet<TemporaryMessage>();
            _sendComplenentionMap = new Dictionary<TemporaryMessage, TaskCompletionSource<bool>>();
            _virtualSynchronyTopic = _communicationManager.Topics.GetPublisher<VirtualSynchronyTopicPublisher>();

            _sendQueue = new AsyncGenericQueue<(Node, Message)>();
            _messageSendStateMap = new Dictionary<Message, TaskCompletionSource<bool>>();

            Topics = new GenericTopicOutlet(this, 
                     new JobGroupPublisher(),
                     new KeepAlivePublisher(),
                     new BullyElectionPublisher());
            JoinRequestTimeout = joinRequestTimeout;
            View = coldStartView ?? new Group(_nodeRegistry.GetOrCreate(id: configurationService.GetValue<int?>("nodeId", null)), coordinator: configurationService.GetValue<bool>("coordinator", false));
        }

        public event Action<Node, Message> OnMessageReceived;

        /// <summary>
        /// This task waits for enqueued messages and sends them appropriatly
        /// </summary>
        /// <returns></returns>
        private async Task MessageRouter(CancellationToken cancellationToken)
        {
            try
            {
                while(!cancellationToken.IsCancellationRequested)
                {
                    var messageToSend = await _sendQueue.Dequeue();

                    if(messageToSend != default)
                    {
                        Node node = messageToSend.Item1;
                        Message message = messageToSend.Item2;

                        try
                        {
                            _logger.Log(Tag.VirtualSynchrony, $"Routing {message.GetType().Name} to {(node?.ToString() ?? "MULTICAST")}");
                            if(node != null) //Unicast
                            {
                                if(View.Contains(node))
                                    await _communicationManager.Send(node, message.ApplyStamp(_messageTimeStamper));
                                else
                                    _logger.Warning(Tag.VirtualSynchrony, $"Message sender has a message for {node} in queue which isn't in the view anymore!");
                            }
                            else //Multicast
                            {
                                await _communicationManager.SendMulticast(message.ApplyStamp(_messageTimeStamper));
                            }
                            
                            //Notify Send Completion
                            _logger.Log(Tag.VirtualSynchrony, $"Routed message {message.GetType().Name}({message.TimeStamp}) to {(node?.ToString() ?? "MULTICAST")}");
                            _messageSendStateMap[message].SetResult(true);
                        }
                        catch(Exception ex)
                        {
                            _logger.Error(Tag.VirtualSynchrony, ex);
                            _messageSendStateMap[message].SetResult(false);
                        }
                        finally
                        {
                            _messageSendStateMap.Remove(message);
                        }
                    }
                }
            }
            catch {}
            finally
            {
                _logger.Log(Tag.VirtualSynchrony, "Message router token got cancelled, stopping send task...");
            }
        }

        public async Task<TemporaryMessage> EnqueueMessageAndWaitSend(Node node, Message message, int timeout)
        {
            //Checks that the node is in the view
            if(node == null || !View.Contains(node))
            {
                NotDeliveredException sendException = new NotDeliveredException();
                _logger.Error(Tag.VirtualSynchrony, $"Tried to send message to node {node.ID} which isn't in view!", sendException);
                throw sendException;
            }

            TemporaryMessage tempMessage = new TemporaryMessage(node == null, message);
            TaskCompletionSource<bool> sendCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _messageSendStateMap[tempMessage] = sendCompletionSource;
            var sendMessageElement = (node, tempMessage);

            lock(View)
            {
                if(_pendingViewChange != null)
                {
                    _logger.Log(Tag.VirtualSynchrony, $"View change in progress, holding message {message.GetType().Name} to {node?.ToString() ?? "MULTICAST"}");
                    _onHoldMessages.Enqueue(sendMessageElement);
                }
                else
                    _sendQueue.Enqueue(sendMessageElement);
            }

            CancellationTokenSource cts = new CancellationTokenSource();
            await Task.WhenAny(sendCompletionSource.Task,
                               Task.Delay(TimeSpan.FromSeconds(timeout), cts.Token));
            cts.Cancel();
            
            if(!sendCompletionSource.Task.IsCompleted || !sendCompletionSource.Task.Result)
            {
                NotDeliveredException sendException = new NotDeliveredException();
                _logger.Error(Tag.VirtualSynchrony, $"Delivery of message {tempMessage.TimeStamp} to {node.ID} failed or timedout!", sendException);
                throw sendException;
            }

            return tempMessage;
        }

        public async Task Send(Node node, Message message, int timeout = 1)
        {
            try
            {
                _logger.Log(Tag.VirtualSynchrony, $"Queuing send {message.GetType().Name} to {node}");
                await EnqueueMessageAndWaitSend(node, message, timeout);
                _logger.Log(Tag.VirtualSynchrony, $"Sent {message.GetType().Name}({message.TimeStamp}) to {node}");
            }
            catch
            {
                throw;
            }
        }

        public async Task SendMulticast(Message message)
        {
            _logger.Log(Tag.VirtualSynchrony, $"Queuing send multicast {message.GetType().Name}");
            TaskCompletionSource<bool> consolidateTask;
            var tempMessage = await EnqueueMessageAndWaitSend(null, message, 30);

            _logger.Log(Tag.VirtualSynchrony, $"Multicast sent on network");
            var messageKey = (View.Me.ID.Value, tempMessage.TimeStamp.Value);
            lock(_confirmationQueue)
            {
                _confirmationQueue.Add(messageKey, tempMessage);
                _sentTemporaryMessages.Add(tempMessage);
                _sendComplenentionMap.Add(tempMessage, (consolidateTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)));
                ProcessAcknowledge(messageKey, View.Me);
            }

            _logger.Log(Tag.VirtualSynchrony, $"Waiting for other acks...");
            //True if every node in the view acknowledged the message
            if(!await consolidateTask.Task)
                throw new MulticastNotDeliveredException();
        }

        private void OnTemporaryMessageReceived(Node node, Message message)
        {
            TemporaryMessage tempMessage = message as TemporaryMessage;
            tempMessage.BindToRegistry(_nodeRegistry);

            //Only care about messages from nodes in my current group view
            if(View.Contains(node))
            {
                _logger.Log(Tag.VirtualSynchrony, $"Received temporary message from {node.ID} with timestamp {message.TimeStamp}");
                var messageKey = (node.ID.Value, tempMessage.TimeStamp.Value);

                lock(_confirmationQueue)
                {
                    _confirmationQueue.Add(messageKey, tempMessage);
                    ProcessAcknowledge(messageKey, node);
                }

                if(tempMessage.IsMulticast)
                {
                    _logger.Log(Tag.VirtualSynchrony, $"Sending ack for received message {messageKey}");
                    _communicationManager.SendMulticast(new TemporaryAckMessage(tempMessage).ApplyStamp(_messageTimeStamper)).Wait();
                    _logger.Log(Tag.VirtualSynchrony, $"Correctly sent ack for {messageKey}");
                }
            }
            else
                _communicationManager.Send(node, new NotInViewMessage(View.Count).ApplyStamp(_messageTimeStamper)).Wait();
        }

        private void OnTemporaryAckReceived(Node node, Message message)
        {
            TemporaryAckMessage tempAckMessage = message as TemporaryAckMessage;
            tempAckMessage.BindToRegistry(_nodeRegistry);

            _logger.Log(Tag.VirtualSynchrony, $"Received acknowledge by {node.ID} of message ({tempAckMessage.OriginalSenderID},{tempAckMessage.OriginalTimestamp})");
            //Only care about messages from nodes in my current group view
            if(View.Contains(node))
            {
                var messageKey = (tempAckMessage.OriginalSenderID, tempAckMessage.OriginalTimestamp);
                ProcessAcknowledge(messageKey, node);   
            }
        }

        private void OnNotInViewReceived(Node node, Message message)
        {
            NotInViewMessage notInViewMessage = message as NotInViewMessage;

            int myViewSize = View.Count;
            if(notInViewMessage.MyViewSize > myViewSize)
            {
                //We need to fault!
                Message teardownMessage = new TeardownMessage().ApplyStamp(_messageTimeStamper);
                _communicationManager.SendMulticast(teardownMessage).Wait();
                OnTeardownReceived(View.Me, teardownMessage);
            }

            if(notInViewMessage.MyViewSize < myViewSize) //The other view needs to fault
                _communicationManager.Send(node, new NotInViewMessage(myViewSize).ApplyStamp(_messageTimeStamper)).Wait();
        }

        private void OnTeardownReceived(Node node, Message message)
        {
            if(View.Contains(node))
            {
                string errorMessage = "Received teardown message, faulting to start from a clean state!";
                _logger.Fatal(Tag.VirtualSynchrony, errorMessage, new Exception(errorMessage));
            }
        }

        ///<summary>
        ///Updates the confirmation map, resets timeouts and consolidates messages that revceived every ack
        ///</summary>
        private void ProcessAcknowledge((int,int) messageKey, Node node)
        {
            lock(_confirmationQueue)
            {
                _logger.Log(Tag.VirtualSynchrony, $"Processing acknowledge by {node.ID} of message ({messageKey.Item1},{messageKey.Item2})");

                bool isUnicast = _confirmationQueue.ContainsKey(messageKey) && !_confirmationQueue[messageKey].IsMulticast;
                if(!_confirmationMap.ContainsKey(messageKey))
                    _confirmationMap.Add(messageKey, new HashSet<Node>( isUnicast ? new HashSet<Node>(new [] { node }) : View.Others));
                
                var confirmationSet = _confirmationMap[messageKey];
                confirmationSet.Remove(node);

                //TODO: Reset timeout here?

                if(confirmationSet.Count == 0)
                    ConsolidateTemporaryMessage(messageKey);
            }
        }

        ///<summary>
        ///Cleans up the state of the hash sets and notifies events
        ///</summary>
        private void ConsolidateTemporaryMessage((int,int) messageKey)
        {
            lock(_confirmationQueue)
            {
                _logger.Log(Tag.VirtualSynchrony, $"Consolidating message ({messageKey.Item1},{messageKey.Item2})");
                _confirmationMap.Remove(messageKey);

                if(!_confirmationQueue.ContainsKey(messageKey))
                {
                    //Pending View Change, the consolidation is related to the removed node ignore
                    if(_pendingViewChange != null && _pendingViewChange.Node.ID == messageKey.Item1)
                        return;
                    else
                        throw new Exception("Consolidated a message that was never received!");
                }

                var message = _confirmationQueue[messageKey];
                _confirmationQueue.Remove(messageKey);
                
                if(_sentTemporaryMessages.Contains(message))
                {
                    //If sender, notify events
                    _sentTemporaryMessages.Remove(message);
                    _sendComplenentionMap[message].SetResult(true);
                    _sendComplenentionMap.Remove(message);
                }
                else
                {
                    //If receiver notify reception
                    Node sender = _nodeRegistry.GetNode(message.SenderID.Value);
                    Task.Run(() => OnMessageReceived?.Invoke(sender, message.UnstablePayload));
                }  
            }
        }

        private void OnFlushMessageReceived(Node node, Message message)
        {
            var flushMessage = message as FlushMessage;
            flushMessage.BindToRegistry(_nodeRegistry);

            if(View.Contains(node) && (flushMessage.RelatedChangeOperation == ViewChangeMessage.ViewChangeOperation.Joined || View.Contains(flushMessage.RelatedChangeNode)))
            {
                _logger.Log(Tag.VirtualSynchrony, $"Received flush message from {node.ID} for change {flushMessage.RelatedChangeNode}{flushMessage.RelatedChangeOperation}");

                lock(View)
                {
                    //Flush messages can arrive before anyone notified this node about the change
                    if(_viewChangeInProgress == null)
                        HandleViewChange(node, new ViewChangeMessage.ViewChange{
                            Node = flushMessage.RelatedChangeNode, 
                            Operation = flushMessage.RelatedChangeOperation
                        }); //Self-Report Viewchange

                    if(_pendingViewChange.IsSame(flushMessage.RelatedChangeNode, flushMessage.RelatedChangeOperation))
                    {
                        _logger.Log(Tag.VirtualSynchrony, $"Processing flush state");
                        _flushedNodes.Add(node);

                        HandleFlushCondition();
                    }
                    else
                        _logger.Warning(Tag.VirtualSynchrony, "Received flush message for another view change!");
                }
            }
        }

        private void OnViewChangeReceived(Node node, Message message)
        {
            var viewChangeMessage = message as ViewChangeMessage;
            viewChangeMessage.BindToRegistry(_nodeRegistry);

            if(View.Contains(node) && (viewChangeMessage.Change.Operation == ViewChangeMessage.ViewChangeOperation.Joined || View.Contains(viewChangeMessage.Change.Node)))
                HandleViewChange(node, viewChangeMessage.Change);
        }

        /// <summary>
        /// Handles a View change notified by someone
        /// </summary>
        /// <param name="viewChangeMessage">Message containing the view change</param>
        private void HandleViewChange(Node initiator, ViewChangeMessage.ViewChange viewChangeMessage)
        {
            //FIXME: These locks on View are probably just bad rapresentation of a view, they should all be included in the view object
            viewChangeMessage.BindToRegistry(_nodeRegistry);

            lock(View)
            {
                _logger.Log(Tag.VirtualSynchrony, $"Handling view change {viewChangeMessage.Node} {viewChangeMessage.Operation} detected by {initiator.ID}");
                //Assume no view change while changing view
                if(_viewChangeInProgress == null)
                {
                    _viewChangeInProgress = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _pendingViewChange = viewChangeMessage;
                    ViewChanging?.Invoke();

                    //Stop all messaging
                    _onHoldMessages = new Queue<(Node, Message)>();

                    if(_pendingViewChange.Operation == ViewChangeMessage.ViewChangeOperation.Left)
                    {
                        //FIXME: Shouldn't we multicast unstable messages from the dead node? Probably handled by timeout (either all or none)
                        //Ignore acknowledges from the dead node
                        lock(_confirmationQueue)
                        {
                            _confirmationMap.Keys.ForEach(messageKey => ProcessAcknowledge(messageKey, viewChangeMessage.Node));
                        }
                    }

                    //Setup Message Flushing and check if we can already flush
                    _flushed = false;
                    _flushedNodes = new HashSet<Node>();
                    _newGroupView = new HashSet<Node>(View.Others);
                    
                    if(viewChangeMessage.Operation == ViewChangeMessage.ViewChangeOperation.Joined)
                        _newGroupView.Add(viewChangeMessage.Node);
                    else
                        _newGroupView.Remove(viewChangeMessage.Node);
                        

                    if(_viewChangeInProgress != null && (initiator == View.Me || initiator != viewChangeMessage.Node) && !_pendingViewChange.IsSame(viewChangeMessage))
                    {
                        //If we got a new viewchange that isn't the same as the one already received
                        //FATAL: Double View Change
                        _viewChangeInProgress.SetResult(false);
                        Exception ex = new Exception("FATAL: ViewChange during view change");
                        _logger.Fatal(Tag.VirtualSynchrony, ex.Message, ex);
                    }

                    if(_viewChangeInProgress != null)
                        HandleFlushCondition();
                }
            }
        }

        //If we are not waiting any more messages from alive nodes we need to consolidate messages
        private bool FlushCondition()
        {
            lock(_confirmationQueue)
            {
                return _pendingViewChange != null && !_confirmationMap.Any(pair => { return !pair.Value.IsSubsetOf(_flushedNodes); });
            }
        }
        private void HandleFlushCondition()
        {
            lock (View)
            {
                if (FlushCondition())
                {
                    AttemptSendFlushMessage();
                    if (_flushedNodes.SetEquals(_pendingViewChange.Operation == ViewChangeMessage.ViewChangeOperation.Left ? _newGroupView : View.Others))
                    {
                        _logger.Log(Tag.VirtualSynchrony, $"All nodes have flushed their messages, consolidating view change");

                        //Check for errors
                        if (_pendingViewChange.Node == View.Me)
                        {
                            var kickedException = new KickedFromViewException();
                            _logger.Fatal(Tag.VirtualSynchrony, kickedException.Message, kickedException);
                        }

                        //Enstablish new View
                        Node coordinator = View.ImCoordinator || _newGroupView.Contains(View.Coordinator) ? View.Coordinator : null;
                        View.Update(_newGroupView, coordinator);
                        var viewChangeTask = _viewChangeInProgress;

                        //Reset view state change
                        _flushed = false;
                        _flushedNodes = null;
                        _pendingViewChange = null;
                        _viewChangeInProgress = null;
                        _newGroupView = null;

                        //Check if it needs to sync to the new node
                        if (_currentJoinRequest != null)
                        {
                            _logger.Log(Tag.VirtualSynchrony, $"Need to sync view with joined node");
                            _communicationManager.Send(_currentJoinRequest.JoiningNode, new ViewSyncResponse(View.Others.ToList()).ApplyStamp(_messageTimeStamper)).Wait();
                            _currentJoinRequest = null;
                        }

                        //Unlock Message Queue
                        _sendQueue.EnqueueRange(_onHoldMessages);
                        _onHoldMessages = null;

                        //Unlock group communication
                        viewChangeTask.SetResult(true);
                    }
                }
            }
        }
        private void AttemptSendFlushMessage()
        {
            if(!_flushed)
            {
                FlushMessage flushMessage = null;
                lock(View)
                {
                    _logger.Log(Tag.VirtualSynchrony, $"I can send my flush message for pending view change {_pendingViewChange.Operation} of {_pendingViewChange.Node}!");
                    flushMessage = new FlushMessage(_pendingViewChange.Node, _pendingViewChange.Operation);
                }
                _communicationManager.SendMulticast(flushMessage.ApplyStamp(_messageTimeStamper)).Wait();
                _flushed = true;
            }
        }

        private void OnJoinRequestReceived(Node node, Message message)
        {
            ViewJoinRequest joinRequest = message as ViewJoinRequest;
            joinRequest.BindToRegistry(_nodeRegistry);

            lock(View)
            {
                //Cannot trigger viewchange due to join during another viewchange
                if(_pendingViewChange != null)
                    return;
            }

            if(!View.Contains(joinRequest.JoiningNode) && _currentJoinRequest == null)
            {
                if(View.Me == View.Coordinator)
                {
                    _logger.Log(Tag.VirtualSynchrony, $"Starting joining procedure");
                    //Only the coordinator processes these
                    _currentJoinRequest = joinRequest;
                    HandleViewChange(View.Me, new ViewChangeMessage.ViewChange{
                        Node = joinRequest.JoiningNode, 
                        Operation = ViewChangeMessage.ViewChangeOperation.Joined
                    });
                }
            }
        }

        private void OnViewSyncReceived(Node coordinator, Message message)
        {
            lock(View)
            {
                ViewSyncResponse viewSyncResponse = message as ViewSyncResponse;
                viewSyncResponse.BindToRegistry(_nodeRegistry);

                HashSet<Node> newView = viewSyncResponse.ViewNodes.ToHashSet();
                
                if(!newView.Contains(View.Me))
                    _logger.Fatal(Tag.VirtualSynchrony, "Received a new view where I'm not included!", new Exception("Received a new view where I'm not included!"));
                newView.Remove(View.Me);
                newView.Add(coordinator);

                View.Update(newView, coordinator);
                _logger.Log(Tag.VirtualSynchrony, $"Received view sync from coordinator {View.Coordinator}{Environment.NewLine}");

                if(!_joinRequestCancellation.Token.IsCancellationRequested)
                    _joinRequestCancellation.Cancel();
            }
        }

        public void NotifyViewChanged(HashSet<Node> nodes, ViewChangeMessage.ViewChangeOperation operation)
        {
            //TODO: Handle multiple nodes in viewchange
            if(View.Contains(nodes))
            {
                HandleViewChange(View.Me, new ViewChangeMessage.ViewChange()
                                {
                                    Node = nodes.First(),
                                    Operation = operation
                                });
            }
        }

        public async void Start()
        {
            _logger.Log(Tag.VirtualSynchrony, "Starting GroupViewManager...");

            //Message Exchange
            _virtualSynchronyTopic.RegisterForMessage(typeof(TemporaryMessage), OnTemporaryMessageReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(TemporaryAckMessage), OnTemporaryAckReceived);

            //View Changes
            _virtualSynchronyTopic.RegisterForMessage(typeof(ViewChangeMessage), OnViewChangeReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(FlushMessage), OnFlushMessageReceived);

            //Group Joining
            _virtualSynchronyTopic.RegisterForMessage(typeof(ViewJoinRequest), OnJoinRequestReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(ViewSyncResponse), OnViewSyncReceived);

            //View Partitioning
            _virtualSynchronyTopic.RegisterForMessage(typeof(NotInViewMessage), OnNotInViewReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(TeardownMessage), OnTeardownReceived);

            //Start Sender
            _senderCancellationTokenSource = new CancellationTokenSource();
            _messageSender = MessageRouter(_senderCancellationTokenSource.Token);

            _logger.Log(Tag.VirtualSynchrony, "Registered for VS messages");
            _joinRequestCancellation = new CancellationTokenSource();
            //Start group join if we are not in a Group
            if(View.Coordinator == null)
            {
                _logger.Log(Tag.VirtualSynchrony, "No coordinator detected, trying to join existing group...");
                do
                {
                    if(!_joinRequestCancellation.Token.IsCancellationRequested)
                    {
                        _logger.Warning(Tag.VirtualSynchrony, "View join request timedout, trying to join...");
                        await _communicationManager.SendMulticast(new ViewJoinRequest(View.Me).ApplyStamp(_messageTimeStamper));
                        
                        try
                        {
                            await Task.Delay(JoinRequestTimeout + (new Random().Next(JoinRequestTimeout)), _joinRequestCancellation.Token); //T + (0,T) random milliseconds
                        }
                        catch 
                        { 
                            _logger.Log(Tag.VirtualSynchrony, "Cancelling join timeout...");
                        }
                    }
                } while(!_joinRequestCancellation.Token.IsCancellationRequested);
                _logger.Log(Tag.VirtualSynchrony, $"Finished procedure sequence!");
            }
            else
                _logger.Log(Tag.VirtualSynchrony, "Coordinator detected, finished startup sequence.");
        }

        public async void Stop()
        {
            _logger.Log(Tag.VirtualSynchrony, "Stopping node, sending view change message");

            if(!_joinRequestCancellation.Token.IsCancellationRequested)
                _joinRequestCancellation.Cancel();

            //TODO: A node advertising he is leaving, might want to double check if we assumed it is possible.
            
            HandleViewChange(View.Me, new ViewChangeMessage.ViewChange{
                            Node = View.Me, 
                            Operation = ViewChangeMessage.ViewChangeOperation.Left
                        });

            if(_viewChangeInProgress != null) await _viewChangeInProgress.Task;
            _senderCancellationTokenSource.Cancel();
        }
    }
}