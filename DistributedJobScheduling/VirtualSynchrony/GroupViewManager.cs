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

namespace DistributedJobScheduling.VirtualSynchrony
{

    //TODO: Timeouts?
    //TODO: Re-Broadcast messages? This is part of the original algorithm but can we get away with dropping them like we are doing?
    //FIXME: Resource Locks? Messages are asynchronous, some blocks should have mutual exclusion
    public class GroupViewManager : IGroupViewManager, ILifeCycle
    {
        public int JoinRequestTimeout { get; set; } = 5000;
        private class MulticastNotDeliveredException : Exception {}
        private class NotDeliveredException : Exception {}

        public Group View { get; private set; }

        public ITopicOutlet Topics { get; private set; }

        private ICommunicationManager _communicationManager;
        private ITimeStamper _messageTimeStamper;
        private ILogger _logger;
        private Node.INodeRegistry _nodeRegistry;
        private ITopicPublisher _virtualSynchronyTopic;

        
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
                                  ILogger logger)
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
            Topics = new GenericTopicOutlet(this, new JobPublisher());
            View = new Group(_nodeRegistry.GetOrCreate(id: configurationService.GetValue<int?>("nodeId", null)), coordinator: configurationService.GetValue<bool>("coordinator", false));
        }

        public event Action<Node, Message> OnMessageReceived;

        private async Task CheckViewChanges()
        {
            if(_viewChangeInProgress != null)
            {
                _logger.Warning(Tag.VirtualSynchrony, "View change in progress, need to await before sending messages");
                if(!await _viewChangeInProgress.Task)
                    throw new MulticastNotDeliveredException(); //FATAL, TODO: Fatal exceptions?
            }
        }

        public async Task Send(Node node, Message message, int timeout = 30)
        {
            await CheckViewChanges();

            _logger.Log(Tag.VirtualSynchrony, $"Start send {message.GetType().Name}");

            //Checks that the node is in the view
            lock(View)
            {
                if(!View.Others.Contains(node))
                {
                    NotDeliveredException sendException = new NotDeliveredException();
                    _logger.Error(Tag.VirtualSynchrony, $"Tried to send message to node {node.ID} which isn't in view!", sendException);
                    throw sendException;
                }
            }

            TemporaryMessage tempMessage = new TemporaryMessage(false, message);
            var messageKey = (View.Me.ID.Value, tempMessage.TimeStamp);
            TaskCompletionSource<bool> sendTask;

            lock(_confirmationQueue)
            {
                _confirmationQueue.Add(messageKey, tempMessage);
                _sentTemporaryMessages.Add(tempMessage);
                _sendComplenentionMap.Add(tempMessage, (sendTask = new TaskCompletionSource<bool>(false)));
                _confirmationMap.Add(messageKey, new HashSet<Node>(new []{ node }));
            }

            await _communicationManager.Send(node, tempMessage, timeout);
            _logger.Log(Tag.VirtualSynchrony, $"Multicast sent on network");

            lock(_confirmationQueue)
            {
                ProcessAcknowledge(messageKey, View.Me);
            }

            //True if node acknowledged the message
            if(!await sendTask.Task)
                throw new NotDeliveredException();
        }

        public async Task SendMulticast(Message message)
        {
            await CheckViewChanges();

            _logger.Log(Tag.VirtualSynchrony, $"Start send multicast {message.GetType().Name}");
            TemporaryMessage tempMessage = new TemporaryMessage(true, message);
            var messageKey = (View.Me.ID.Value, tempMessage.TimeStamp);
            TaskCompletionSource<bool> sendTask;

            lock(_confirmationQueue)
            {
                _confirmationQueue.Add(messageKey, tempMessage);
                _sentTemporaryMessages.Add(tempMessage);
                _sendComplenentionMap.Add(tempMessage, (sendTask = new TaskCompletionSource<bool>(false)));
            }

            await _communicationManager.SendMulticast(tempMessage);
            _logger.Log(Tag.VirtualSynchrony, $"Multicast sent on network");

            lock(_confirmationQueue)
            {
                ProcessAcknowledge(messageKey, View.Me);
            }

            //True if every node in the view acknowledged the message
            if(!await sendTask.Task)
                throw new MulticastNotDeliveredException();
        }

        private void OnTemporaryMessageReceived(Node node, Message message)
        {
            TemporaryMessage tempMessage = message as TemporaryMessage;
            tempMessage.BindToRegistry(_nodeRegistry);

            //Only care about messages from nodes in my current group view
            lock(View)
            {
                if(View.Others.Contains(node))
                {
                    var messageKey = (node.ID.Value, tempMessage.TimeStamp);
                    lock(_confirmationQueue)
                    {
                        _confirmationQueue.Add(messageKey, tempMessage);

                        if(tempMessage.IsMulticast)
                            _communicationManager.SendMulticast(new TemporaryAckMessage(tempMessage, _messageTimeStamper)).Wait();
                        else
                            _communicationManager.Send(node, new TemporaryAckMessage(tempMessage, _messageTimeStamper)).Wait();

                        ProcessAcknowledge(messageKey, node);
                    }
                }
                else
                    _communicationManager.Send(node, new NotInViewMessage(View.Others.Count + 1, _messageTimeStamper)).Wait();
            }
        }

        private void OnNotInViewReceived(Node node, Message message)
        {
            NotInViewMessage notInViewMessage = message as NotInViewMessage;

            int myViewSize = 0;
            lock(View)
            {
                myViewSize = (View.Others.Count + 1);
            }

            if(notInViewMessage.MyViewSize > myViewSize)
            {
                //We need to fault!
                TeardownMessage teardownMessage = new TeardownMessage(_messageTimeStamper);
                _communicationManager.SendMulticast(teardownMessage).Wait();
                OnTeardownReceived(View.Me, teardownMessage);
            }

            if(notInViewMessage.MyViewSize < myViewSize) //The other view needs to fault
                _communicationManager.Send(node, new NotInViewMessage(myViewSize, _messageTimeStamper)).Wait();
        }

        private void OnTeardownReceived(Node node, Message message)
        {
            lock(View)
            {
                if(View.Others.Contains(node))
                {
                    string errorMessage = "Received teardown message, faulting to start from a clean state!";
                    _logger.Fatal(Tag.VirtualSynchrony, errorMessage, new Exception(errorMessage));
                }
            }
        }

        private void OnTemporaryAckReceived(Node node, Message message)
        {
            TemporaryAckMessage tempAckMessage = message as TemporaryAckMessage;
            tempAckMessage.BindToRegistry(_nodeRegistry);

            lock(View)
            {
                //Only care about messages from nodes in my current group view
                if(View.Others.Contains(node))
                {
                    var messageKey = (tempAckMessage.OriginalSenderID, tempAckMessage.OriginalTimestamp);
                    ProcessAcknowledge(messageKey, node);   
                }
            }
        }

        ///<summary>
        ///Updates the confirmation map, resets timeouts and consolidates messages that revceived every ack
        ///</summary>
        private void ProcessAcknowledge((int,int) messageKey, Node node)
        {
            lock(View)
            {
                lock(_confirmationQueue)
                {
                    _logger.Log(Tag.VirtualSynchrony, $"Processing acknoledge by {node.ID} of message ({messageKey.Item1},{messageKey.Item2})");

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
                    OnMessageReceived?.Invoke(sender, message.UnstablePayload);
                }  
            }
        }

        private void OnFlushMessageReceived(Node node, Message message)
        {
            var flushMessage = message as FlushMessage;
            flushMessage.BindToRegistry(_nodeRegistry);

            lock(View)
            {
                if(View.Others.Contains(node))
                {
                    _logger.Log(Tag.VirtualSynchrony, $"Received flush message from {node.ID} for change {flushMessage.RelatedChangeNode}{flushMessage.RelatedChangeOperation}");
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

            lock(View)
            {
                if(View.Others.Contains(node))
                    HandleViewChange(node, viewChangeMessage.Change);
            }
        }

        /// <summary>
        /// Handles a View change notified by someone
        /// </summary>
        /// <param name="viewChangeMessage">Message containing the view change</param>
        private void HandleViewChange(Node initiator, ViewChangeMessage.ViewChange viewChangeMessage)
        {
            viewChangeMessage.BindToRegistry(_nodeRegistry);

            lock(View)
            {
                _logger.Log(Tag.VirtualSynchrony, $"Handling view change {viewChangeMessage.Node} {viewChangeMessage.Operation} detected by {initiator.ID}");
                //Assume no view change while changing view
                if(_viewChangeInProgress == null)
                {
                    _viewChangeInProgress = new TaskCompletionSource<bool>();
                    _pendingViewChange = viewChangeMessage;

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

                    HandleFlushCondition(); 
                }
                else if((initiator == View.Me || initiator != viewChangeMessage.Node) && !_pendingViewChange.IsSame(viewChangeMessage))
                {
                    //If we got a new viewchange that isn't the same as the one already received
                    //FATAL: Double View Change
                    _viewChangeInProgress.SetResult(false);
                    throw new Exception("FATAL: ViewChange during view change");
                }
            }
        }

        //If we are not waiting any more messages from alive nodes we need to consolidate messages
        private bool FlushCondition() => _pendingViewChange != null && !_confirmationMap.Any(pair => { return !pair.Value.IsSubsetOf(_flushedNodes); });
        private void HandleFlushCondition()
        {
            lock(View)
            {
                lock(_confirmationQueue)
                {
                    if(FlushCondition())
                    {
                        if(!_flushed)
                        {
                            _logger.Log(Tag.VirtualSynchrony, $"I can send my flush message for pending view change {_pendingViewChange.Operation} of {_pendingViewChange.Node}!");
                            _communicationManager.SendMulticast(new FlushMessage(_pendingViewChange.Node, _pendingViewChange.Operation, _messageTimeStamper)).Wait();
                            _flushed = true;
                        }

                        if(_flushedNodes.SetEquals(_pendingViewChange.Operation == ViewChangeMessage.ViewChangeOperation.Left ? _newGroupView : View.Others))
                        {
                            _logger.Log(Tag.VirtualSynchrony, $"All nodes have flushed their messages, consolidating view change");
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
                            if(_currentJoinRequest != null)
                            {
                                _logger.Log(Tag.VirtualSynchrony, $"Need to sync view with joined node");
                                _communicationManager.Send(_currentJoinRequest.JoiningNode, new ViewSyncResponse(View.Others.ToList(), _messageTimeStamper)).Wait();
                                _currentJoinRequest = null;
                            }

                            //Unlock group communication
                            viewChangeTask.SetResult(true);
                        }
                    }
                }
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

                if(!View.Others.Contains(joinRequest.JoiningNode) && _currentJoinRequest == null)
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

        public void Init()
        {
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
                        await _communicationManager.SendMulticast(new ViewJoinRequest(View.Me, _messageTimeStamper));
                        
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
            
            HandleViewChange(View.Me, new ViewChangeMessage.ViewChange{
                            Node = View.Me, 
                            Operation = ViewChangeMessage.ViewChangeOperation.Left
                        });
        }
    }
}