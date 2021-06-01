using System.Collections.Concurrent;
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
using DistributedJobScheduling.DistributedJobUpdate;
using DistributedJobScheduling.Communication.Messaging.Discovery;
using static DistributedJobScheduling.Communication.NetworkManager;

namespace DistributedJobScheduling.VirtualSynchrony
{
    public class GroupViewManager : IGroupViewManager, IStartable
    {
        private const int DEFAULT_SEND_TIMEOUT = 30;

        public int JoinRequestTimeout { get; set; } = 5000;
        
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
        private Thread _messageSender;
        private BlockingCollection<(Node, Message, SendFailureStrategy, Action)> _sendQueue;
        private Dictionary<Message, TaskCompletionSource<bool>> _messageSendStateMap;
        private ConcurrentQueue<(Node, Message, SendFailureStrategy, Action)> _onHoldMessages;

        
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
        private ViewChange _pendingViewChange;
        private TaskCompletionSource<bool> _viewChangeInProgress;
        private HashSet<Node> _newGroupView;
        private HashSet<Node> _flushedNodes;
        private Queue<(Node, ViewMessage)> _futureMessagesQueue;
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
            _futureMessagesQueue = new Queue<(Node, ViewMessage)>();
            _virtualSynchronyTopic = _communicationManager.Topics.GetPublisher<VirtualSynchronyTopicPublisher>();

            _sendQueue = new BlockingCollection<(Node, Message, SendFailureStrategy, Action)>();
            _messageSendStateMap = new Dictionary<Message, TaskCompletionSource<bool>>();

            Topics = new GenericTopicOutlet(this, logger,
                     new JobGroupPublisher(),
                     new KeepAlivePublisher(),
                     new BullyElectionPublisher(),
                     new DistributedJobUpdatePublisher());
            JoinRequestTimeout = joinRequestTimeout;
            View = coldStartView ?? new Group(_nodeRegistry.GetOrCreate(id: configurationService.GetValue<int?>("nodeId", null)), coordinator: configurationService.GetValue<bool>("coordinator", false));
            View.MemberDied += (node) => { NotifyViewChanged(new HashSet<Node>(new [] {node}), Operation.Left); };
        }

        public event Action<Node, Message> OnMessageReceived;

        /// <summary>
        /// Checks if the message belongs to the current view
        /// </summary>
        /// <returns>true if the message needs to be process now, false otherwise</returns>
        private bool DiscriminateMessage(Node node, ViewMessage message)
        {
            lock(View)
            {
                //TODO: Maybe a better way than record all messages since start?
                if(!View.ViewId.HasValue || message.ViewId > View.ViewId) //Future Message
                {
                    _futureMessagesQueue.Enqueue((node, message));
                }

                if(message.ViewId < View.ViewId)
                    _logger.Log(Tag.VirtualSynchrony, $"Discarding duplicate message {message.GetType().Name} from {node} for old view {message.ViewId}");

                return message.ViewId == View.ViewId;
            }
        }

        /// <summary>
        /// This thread waits for enqueued messages and sends them appropriatly
        /// </summary>
        /// <returns></returns>
        private void MessageRouter(CancellationToken cancellationToken)
        {
            try
            {
                while(!cancellationToken.IsCancellationRequested)
                {
                    var messageToSend = _sendQueue.Take(cancellationToken);

                    if(messageToSend != default)
                    {
                        Node node = messageToSend.Item1;
                        Message message = messageToSend.Item2;
                        SendFailureStrategy strategy = messageToSend.Item3;
                        Action callback = messageToSend.Item4;

                        try
                        {
                            _logger.Log(Tag.VirtualSynchrony, $"Routing {message.GetType().Name} to {(node?.ToString() ?? "MULTICAST")}");
                            if(node != null) //Unicast
                            {
                                if(View.Contains(node))
                                {
                                    if(View.Me != node)
                                        _communicationManager.Send(node, message.ApplyStamp(_messageTimeStamper), strategy).Wait();
                                    else //LoopBack
                                        Task.Run(() => _virtualSynchronyTopic.RouteMessage(message.GetType(), node, message));
                                }
                                else
                                    _logger.Warning(Tag.VirtualSynchrony, $"Message sender has a message for {node} in queue which isn't in the view anymore!");
                            }
                            else //Multicast
                            {
                                List<Node> viewMembers = new List<Node>(View.Others);

                                if(viewMembers.Count > 0)
                                {
                                    Task[] awaitMulticast = new Task[viewMembers.Count];
                                    Message timeStampedMessage = message.ApplyStamp(_messageTimeStamper);
                                    
                                    //Reliable Multicast
                                    for(int i = 0; i <  viewMembers.Count; i++)
                                        awaitMulticast[i] = _communicationManager.Send(viewMembers[i], timeStampedMessage, strategy);
                                    
                                    Task.WhenAll(awaitMulticast).Wait();
                                }
                            }
                            
                            //Notify Send Completion
                            _logger.Log(Tag.VirtualSynchrony, $"Routed message {message.GetType().Name}({message.TimeStamp}) to {(node?.ToString() ?? "MULTICAST")}");
                            
                            callback?.Invoke();

                            if(_messageSendStateMap.ContainsKey(message))
                                _messageSendStateMap[message].TrySetResult(true);
                        }
                        catch(Exception ex)
                        {
                            _logger.Error(Tag.VirtualSynchrony, ex);
                            if(_messageSendStateMap.ContainsKey(message))
                                _messageSendStateMap[message].TrySetResult(false);
                        }
                        finally
                        {
                            if(_messageSendStateMap.ContainsKey(message))
                                _messageSendStateMap.Remove(message);
                        }
                    }
                }
            }
            catch(Exception ex) {
                if(!cancellationToken.IsCancellationRequested)
                    _logger.Fatal(Tag.VirtualSynchrony, "Message router errored out", ex);
            }
            finally
            {
                if(cancellationToken.IsCancellationRequested)
                    _logger.Warning(Tag.VirtualSynchrony, "Message router token got cancelled, stopping send task...");
            }
        }

        private (TemporaryMessage, TaskCompletionSource<bool>) EnqueueMessage(Node node, Message message, SendFailureStrategy sendFailureStrategy, CancellationToken token = default)
        {
            //Checks that the node is in the view
            if(node != null && !View.Contains(node))
            {
                NotDeliveredException sendException = new NotDeliveredException();
                _logger.Error(Tag.VirtualSynchrony, $"Tried to send message to node {node.ID} which isn't in view!", sendException);
                throw sendException;
            }

            TemporaryMessage tempMessage;
            TaskCompletionSource<bool> sendCompletionSource;
            lock(View)
            {
                tempMessage = new TemporaryMessage(node == null, message, View.ViewId.Value);
                sendCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() => {sendCompletionSource.TrySetResult(false); });
                _messageSendStateMap[tempMessage] = sendCompletionSource;
                var sendMessageElement = (node, tempMessage, sendFailureStrategy, default(Action));

                if(_pendingViewChange != null)
                {
                    _logger.Log(Tag.VirtualSynchrony, $"View change in progress, holding message {message.GetType().Name} to {node?.ToString() ?? "MULTICAST"}");
                    _onHoldMessages.Enqueue(sendMessageElement);
                }
                else
                    _sendQueue.Add(sendMessageElement);
            }

            return (tempMessage, sendCompletionSource);
        }

        private async Task<TemporaryMessage> EnqueueMessageAndWaitSend(Node node, Message message, SendFailureStrategy sendFailureStrategy, int timeout = DEFAULT_SEND_TIMEOUT)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            _logger.Log(Tag.VirtualSynchrony, $"Queuing send {message.GetType().Name} to {(node?.ToString() ?? "MULTICAST")}");
            var sendMessageTask = EnqueueMessage(node, message, sendFailureStrategy, cts.Token);

            _logger.Log(Tag.VirtualSynchrony, $"Queued send {message.GetType().Name} to {(node?.ToString() ?? "MULTICAST")}");
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            await sendMessageTask.Item2.Task;
            
            if(!sendMessageTask.Item2.Task.IsCompleted || !sendMessageTask.Item2.Task.Result)
            {
                NotDeliveredException sendException = new NotDeliveredException();
                _logger.Error(Tag.VirtualSynchrony, $"Delivery of message {sendMessageTask.Item1.TimeStamp} to {(node?.ToString() ?? "MULTICAST")} failed or timedout!", sendException);
                throw sendException;
            }

            return sendMessageTask.Item1;
        }

        public async Task Send(Node node, Message message, SendFailureStrategy strategy)
        {
            if(node == null)
            {
                _logger.Error(Tag.VirtualSynchrony, "Trying to send a message to null node");
                return;
            }
            
            try
            {
                await EnqueueMessageAndWaitSend(node, message, strategy, DEFAULT_SEND_TIMEOUT);
                _logger.Log(Tag.VirtualSynchrony, $"Sent {message.GetType().Name}({View.Me.ID},{message.TimeStamp}) to {node}");
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Sends a reliable multicast to each view member
        /// </summary>
        /// <param name="message">Message to send</param>
        public async Task SendMulticast(Message message)
        {
            if(View.Others.Count == 0)
                return;
            
            _logger.Log(Tag.VirtualSynchrony, $"Queuing send multicast {message.GetType().Name}");
            TaskCompletionSource<bool> consolidateTask;
            var tempMessage = await EnqueueMessageAndWaitSend(null, message, SendFailureStrategy.Discard, 30);

            _logger.Log(Tag.VirtualSynchrony, $"Multicast sent on network ({View.Me.ID},{message.TimeStamp})");
            var messageKey = (View.Me.ID.Value, tempMessage.TimeStamp.Value);
            lock(View)
            {
                _confirmationQueue.Add(messageKey, tempMessage);
                _sentTemporaryMessages.Add(tempMessage);
                _sendComplenentionMap.Add(tempMessage, (consolidateTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)));
                ProcessAcknowledge(messageKey, View.Me);
            }

            _logger.Log(Tag.VirtualSynchrony, $"Waiting for other acks ({View.Me.ID},{message.TimeStamp})...");
            //True if every node in the view acknowledged the message
            if(!await consolidateTask.Task)
                throw new MulticastNotDeliveredException();
            _logger.Log(Tag.VirtualSynchrony, $"Consolidation task finished ({View.Me.ID},{message.TimeStamp})...");
        }

        private void OnTemporaryMessageReceived(Node node, Message message)
        {
            TemporaryMessage tempMessage = message as TemporaryMessage;
            tempMessage.BindToRegistry(_nodeRegistry);

            if (!DiscriminateMessage(node, tempMessage))
                return;

            //Only care about messages from nodes in my current group view
            if (View.Contains(node))
            {
                
                if(node == View.Me) //Loop-Back
                {
                    _logger.Log(Tag.VirtualSynchrony, $"Loopback message of type {tempMessage.UnstablePayload.GetType()}");
                    Task.Run(() => OnMessageReceived?.Invoke(node, tempMessage.UnstablePayload));
                    return;
                }

                _logger.Log(Tag.VirtualSynchrony, $"Received temporary message from {node.ID} with timestamp {message.TimeStamp}");
                var messageKey = (node.ID.Value, tempMessage.TimeStamp.Value);
                lock (View)
                {
                    _confirmationQueue.Add(messageKey, tempMessage);
                    ProcessAcknowledge(messageKey, node);
                }

                if (tempMessage.IsMulticast)
                {
                    _logger.Log(Tag.VirtualSynchrony, $"Sending ack for received message {messageKey}");
                    _sendQueue.Add((null, new TemporaryAckMessage(tempMessage), SendFailureStrategy.Discard, null));
                    _logger.Log(Tag.VirtualSynchrony, $"Correctly sent ack for {messageKey}");
                }
            }
            else
                _communicationManager.Send(node, new NotInViewMessage(View.Count, View.ViewId)).Wait();
        }

        private void OnTemporaryAckReceived(Node node, Message message)
        {
            TemporaryAckMessage tempAckMessage = message as TemporaryAckMessage;
            tempAckMessage.BindToRegistry(_nodeRegistry);

            if(!DiscriminateMessage(node, tempAckMessage))
                return;

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
            int? myViewId = View.ViewId;
            if(notInViewMessage.MyViewId > myViewId || notInViewMessage.MyViewSize > myViewSize)
            {
                //We need to fault!
                Message teardownMessage = new TeardownMessage();
                _communicationManager.SendMulticast(teardownMessage).Wait(); //Unrealiable Multicast
                OnTeardownReceived(View.Me, teardownMessage);
            } else //The other view needs to fault
                _communicationManager.Send(node, new NotInViewMessage(myViewSize, myViewId)).Wait();
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
            lock(View)
            {
                _logger.Log(Tag.VirtualSynchrony, $"Processing acknowledge by {node.ID} of message ({messageKey.Item1},{messageKey.Item2})");

                bool isUnicast = _confirmationQueue.ContainsKey(messageKey) && !_confirmationQueue[messageKey].IsMulticast;
                if(!_confirmationMap.ContainsKey(messageKey))
                {
                    _confirmationMap.Add(messageKey, new HashSet<Node>( isUnicast ? new HashSet<Node>(new [] { node }) : View.Others));
                    
                    if(!isUnicast && messageKey.Item1 == View.Me.ID) //If I'm the sender I should logically ack my message
                        _confirmationMap[messageKey].Add(View.Me);
                }
                
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
            lock(View)
            {
                _logger.Log(Tag.VirtualSynchrony, $"Consolidating message ({messageKey.Item1},{messageKey.Item2})");
                _confirmationMap.Remove(messageKey);

                if(!_confirmationQueue.ContainsKey(messageKey))
                {
                    //Pending View Change, the consolidation is related to the removed node ignore
                    if(_pendingViewChange != null && _pendingViewChange.Changes.ContainsKey(_nodeRegistry.GetNode(messageKey.Item1)))
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
                    Node sender = _nodeRegistry.GetNode(message.SenderID);
                    Task.Run(() => OnMessageReceived?.Invoke(sender, message.UnstablePayload));
                }  
            }
        }

        private void OnFlushMessageReceived(Node node, Message message)
        {
            var flushMessage = message as FlushMessage;
            flushMessage.BindToRegistry(_nodeRegistry);

            if(!DiscriminateMessage(node, flushMessage))
                return;
            
            if(View.Contains(node))
            {
                _logger.Log(Tag.VirtualSynchrony, $"Received flush message from {node.ID} for change {flushMessage.RelatedChange}");

                lock(View)
                {
                    //Flush messages can arrive before anyone notified this node about the change
                    if(_viewChangeInProgress == null)
                        HandleViewChange(node, flushMessage.RelatedChange); //Self-Report Viewchange
                    else if(_pendingViewChange?.IsSame(flushMessage.RelatedChange) == true)
                    {
                        _logger.Log(Tag.VirtualSynchrony, $"Processing flush state");
                        _flushedNodes.Add(node);

                        MergeChangesAndProcessAcknowledges(flushMessage.RelatedChange);

                        HandleFlushCondition();
                    }
                    else
                        _logger.Warning(Tag.VirtualSynchrony, "Received flush message for another view change!");
                }
            }
        }

        /// <summary>
        /// Handles a View change notified by someone
        /// </summary>
        /// <param name="viewChange">Message containing the view change</param>
        private void HandleViewChange(Node initiator, ViewChange viewChange)
        {
            //FIXME: These locks on View are probably just bad rapresentation of a view, they should all be included in the view object
            viewChange.BindToRegistry(_nodeRegistry);

            lock(View)
            {
                _logger.Log(Tag.VirtualSynchrony, $"Handling view change {viewChange} detected by {initiator.ID}");
                //Assume no view change while changing view
                if(_viewChangeInProgress == null)
                {
                    _viewChangeInProgress = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    
                    ViewChanging?.Invoke();

                    //Stop all messaging
                    _onHoldMessages = new ConcurrentQueue<(Node, Message, SendFailureStrategy, Action)>();

                    //Setup Message Flushing and check if we can already flush
                    _flushed = false;
                    _flushedNodes = new HashSet<Node>();
                    _flushedNodes.Add(initiator);

                    MergeChangesAndProcessAcknowledges(viewChange);
                    
                    HandleFlushCondition();
                }
            }
        }

        private void MergeChangesAndProcessAcknowledges(ViewChange otherChange)
        {
            lock(View)
            {
                //Always merge changes
                var viewDifferences = _pendingViewChange?.Difference(otherChange) ?? otherChange.Changes;

                foreach(var difference in viewDifferences)
                {
                    _logger.Log(Tag.VirtualSynchrony, $"Processing difference CurrentView({View.ViewId}): {{'{difference.Key}': {difference.Value}}}");
                    if(difference.Value == Operation.Left)
                    {
                        //FIXME: Shouldn't we multicast unstable messages from the dead node? Probably handled by timeout (either all or none)
                        //Ignore acknowledges from the dead node
                        _confirmationMap.Keys.ToArray().ForEach(messageKey => ProcessAcknowledge(messageKey, difference.Key));
                    }
                }

                if(_pendingViewChange != null)
                    _pendingViewChange.Merge(otherChange);
                else
                    _pendingViewChange = otherChange;
                
                _newGroupView = _pendingViewChange.Apply(View.Others);
            }
        }

        //If we are not waiting any more messages from alive nodes we need to consolidate messages
        private void HandleFlushCondition()
        {
            lock (View)
            {
                bool cantConsolidatMoreMessages = !_confirmationMap.Any(pair => { return !pair.Value.IsSubsetOf(_flushedNodes); });
                if (_pendingViewChange != null && cantConsolidatMoreMessages)
                {
                    AttemptSendFlushMessage();
                    var confirmationSet = _pendingViewChange.Apply(View.Others, true);
                    if (_flushedNodes.IsSupersetOf(confirmationSet))
                    {
                        _logger.Log(Tag.VirtualSynchrony, $"All nodes have flushed their messages, consolidating view change");

                        //Check for errors
                        if (_pendingViewChange.Changes.ContainsKey(View.Me) && _pendingViewChange.Changes[View.Me] == Operation.Left)
                        {
                            var kickedException = new KickedFromViewException();
                            _logger.Fatal(Tag.VirtualSynchrony, kickedException.Message, kickedException);
                            return;
                        }

                        //Enstablish new View
                        Node coordinator = View.ImCoordinator || _newGroupView.Contains(View.Coordinator) ? View.Coordinator : null;
                        View.Update(_newGroupView, coordinator, _pendingViewChange.ViewId);
                        _logger.Log(Tag.VirtualSynchrony, $"Consolidated view {View.ViewId}: COORD => [{coordinator}], OTHERS => [{string.Join(",", _newGroupView)}]");
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
                            _currentJoinRequest.BindToRegistry(_nodeRegistry);

                            _logger.Log(Tag.VirtualSynchrony, $"Querying each view statefull component...");

                            //FIXME: View syncs won't work like this during unit testing
                            var componentsSyncMessages = new Dictionary<Type, Message>();
                            DependencyManager.Implementing<IViewStatefull>().ForEach(statefull => 
                                componentsSyncMessages.Add(statefull.GetType(), statefull.ToSyncMessage())
                            );
                            
                            _logger.Log(Tag.VirtualSynchrony, $"Sending view sync response");
                            _sendQueue.Add((_currentJoinRequest.JoiningNode, new ViewSyncResponse(View.Others.ToList(), View.ViewId.Value, componentsSyncMessages), SendFailureStrategy.Reconnect, () => {
                                _currentJoinRequest = null;
                            }));
                        }
                        

                        //Unlock Message Queue
                        foreach(var message in _onHoldMessages)
                            _sendQueue.Add(message);
                        _onHoldMessages = null;

                        //Unlock group communication
                        viewChangeTask.SetResult(true);

                        //Process Future Messages
                        Task.Run(ProcessFutureMessages);
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
                    _logger.Log(Tag.VirtualSynchrony, $"I can send my flush message for pending view change {_pendingViewChange}!");
                    flushMessage = new FlushMessage(_pendingViewChange, _pendingViewChange.ViewId.Value - 1);
                }
                _sendQueue.Add((null, flushMessage, SendFailureStrategy.Reconnect, default(Action)));
                _flushed = true;
            }
        }

        public void ProcessFutureMessages()
        {
            Queue<(Node, ViewMessage)> messageToRouterAgain;

            lock(View)
            {
                messageToRouterAgain = new Queue<(Node, ViewMessage)>(_futureMessagesQueue);
                _futureMessagesQueue.Clear();

                _logger.Log(Tag.VirtualSynchrony, $"Replaying { messageToRouterAgain?.Count } messages after view change");

                messageToRouterAgain.ForEach((futureMessage) => {
                    _virtualSynchronyTopic.RouteMessage(futureMessage.Item2.GetType(), futureMessage.Item1, futureMessage.Item2);
                });
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

                if(!View.Contains(joinRequest.JoiningNode) && _currentJoinRequest == null)
                {
                    if(View.Me == View.Coordinator)
                    {
                        _logger.Log(Tag.VirtualSynchrony, $"Starting joining procedure");
                        //Only the coordinator processes these
                        _currentJoinRequest = joinRequest;
                        Task.Run(() => _communicationManager.SendMulticast(new NetworkStateMessage(View.Others.Union(new [] { View.Me }).ToArray())));
                        NotifyViewChanged(new HashSet<Node>(new [] {joinRequest.JoiningNode}), Operation.Joined);
                    }
                }
            }
        }

        private void OnViewSyncReceived(Node coordinator, Message message)
        {
            ViewSyncResponse viewSyncResponse = message as ViewSyncResponse;
            viewSyncResponse.BindToRegistry(_nodeRegistry);

            lock(View)
            {
                HashSet<Node> newView = viewSyncResponse.ViewNodes.ToHashSet();
                
                if(!newView.Contains(View.Me))
                    _logger.Fatal(Tag.VirtualSynchrony, "Received a new view where I'm not included!", new Exception("Received a new view where I'm not included!"));
                newView.Remove(View.Me);

                if(coordinator != null && coordinator != View.Me)
                    newView.Add(coordinator);

                View.Update(newView, coordinator, id: viewSyncResponse.ViewId);
                _logger.Log(Tag.VirtualSynchrony, $"Received view sync from coordinator {View.Coordinator}");
                _logger.Log(Tag.VirtualSynchrony, $"Join complete with view {View.ViewId}: COORD => [{View.Coordinator}], OTHERS => [{string.Join(",", View.Others)}]");
                

                if(!_joinRequestCancellation.Token.IsCancellationRequested)
                    _joinRequestCancellation.Cancel();

                _logger.Log(Tag.VirtualSynchrony, $"Sync state for statefull components");
                DependencyManager.Implementing<IViewStatefull>().ForEach(
                    statefull => statefull.OnViewSync(viewSyncResponse.ViewStates[statefull.GetType()])
                );
                //In case some messages were received before the viewsync
                new Thread(ProcessFutureMessages).Start();
            }
        }

        public void NotifyViewChanged(HashSet<Node> nodes, Operation operation)
        {
            //TODO: Handle multiple nodes in viewchange
            var viewChanges = new Dictionary<Node, Operation>(nodes.Count);
            foreach(Node node in nodes)
                if(operation == Operation.Joined || View.Contains(node))
                    viewChanges.Add(node, operation);

            var viewChange = new ViewChange()
            {
                Changes = viewChanges,
                ViewId = View.ViewId + 1
            };
            viewChange.BindToRegistry(_nodeRegistry);

            HandleViewChange(
                View.Me, 
                viewChange
            );
        }

        public async void Start()
        {
            _logger.Log(Tag.VirtualSynchrony, "Starting GroupViewManager...");

            //Message Exchange
            _virtualSynchronyTopic.RegisterForMessage(typeof(TemporaryMessage), OnTemporaryMessageReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(TemporaryAckMessage), OnTemporaryAckReceived);

            //View Changes
            _virtualSynchronyTopic.RegisterForMessage(typeof(FlushMessage), OnFlushMessageReceived);

            //Group Joining
            _virtualSynchronyTopic.RegisterForMessage(typeof(ViewJoinRequest), OnJoinRequestReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(ViewSyncResponse), OnViewSyncReceived);

            //View Partitioning
            _virtualSynchronyTopic.RegisterForMessage(typeof(NotInViewMessage), OnNotInViewReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(TeardownMessage), OnTeardownReceived);

            //Start Sender
            _senderCancellationTokenSource = new CancellationTokenSource();
            _messageSender = new Thread(() => MessageRouter(_senderCancellationTokenSource.Token));
            _messageSender.Start();

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
                        await _communicationManager.SendMulticast(new ViewJoinRequest(View.Me)); //Unrealiable Multicast
                        
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

        public void Stop()
        {
            _logger.Log(Tag.VirtualSynchrony, "Stopping node, sending view change message");

            if(!_joinRequestCancellation.Token.IsCancellationRequested)
                _joinRequestCancellation.Cancel();

            //TODO: A node advertising he is leaving, might want to double check if we assumed it is possible.
            //NotifyViewChanged(new HashSet<Node>(new [] { View.Me }), Operation.Left);
            
            //We can avoid waiting for the process to terminate correctly
            _senderCancellationTokenSource.Cancel();
        }
    }
}