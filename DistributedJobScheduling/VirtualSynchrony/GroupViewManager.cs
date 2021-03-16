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
    //FIXME: Resource Locks? Messages are asynchronous, some blocks should have mutual exclusion
    public class GroupViewManager : IGroupViewManager, ILifeCycle
    {
        private class MulticastNotDeliveredException : Exception {}

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

        private TaskCompletionSource<bool> _joinRequestCompletion;

        private ViewJoinRequest _currentJoinRequest;
        private ViewChangeMessage _pendingViewChange;
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
                if(!await _viewChangeInProgress.Task)
                    throw new MulticastNotDeliveredException(); //FATAL, TODO: Fatal exceptions?
        }

        public async Task Send(Node node, Message message, int timeout = 30)
        {
            await CheckViewChanges();
            await _communicationManager.Send(node, message, timeout);
        }

        public async Task SendMulticast(Message message)
        {
            await CheckViewChanges();

            _logger.Log(Tag.VirtualSynchrony, $"Start send multicast {message.GetType().Name}");
            TemporaryMessage tempMessage = new TemporaryMessage(message);
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

            //Only care about messages from nodes in my current group view
            lock(View)
            {
                if(View.Others.Contains(node))
                {
                    var messageKey = (node.ID.Value, tempMessage.TimeStamp);
                    lock(_confirmationQueue)
                    {
                        _confirmationQueue.Add(messageKey, tempMessage);

                        _communicationManager.SendMulticast(new TemporaryAckMessage(tempMessage, _messageTimeStamper));

                        ProcessAcknowledge(messageKey, node);
                    }
                }
            }
        }

        private void OnTemporaryAckReceived(Node node, Message message)
        {
            TemporaryAckMessage tempAckMessage = message as TemporaryAckMessage;
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

                    if(!_confirmationMap.ContainsKey(messageKey))
                        _confirmationMap.Add(messageKey, new HashSet<Node>(View.Others));
                    
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

            lock(View)
            {
                if(View.Others.Contains(node))
                {
                    _logger.Log(Tag.VirtualSynchrony, $"Received flush message from {node.ID}");
                    //Flush messages can arrive before anyone notified this node about the change
                    if(_viewChangeInProgress == null)
                        HandleViewChange(View.Me, new ViewChangeMessage(flushMessage.RelatedChangeNode, flushMessage.RelatedChangeOperation)); //Self-Report Viewchange

                    _flushedNodes.Add(node);

                    HandleFlushCondition();
                }
            }
        }

        private void OnViewChangeReceived(Node node, Message message)
        {
            var viewChangeMessage = message as ViewChangeMessage;

            lock(View)
            {
                if(View.Others.Contains(node))
                    HandleViewChange(node, viewChangeMessage);
            }
        }

        /// <summary>
        /// Handles a View change notified by someone
        /// </summary>
        /// <param name="viewChangeMessage">Message containing the view change</param>
        private void HandleViewChange(Node initiator, ViewChangeMessage viewChangeMessage)
        {
            viewChangeMessage.BindToRegistry(_nodeRegistry);

            lock(View)
            {
                _logger.Log(Tag.VirtualSynchrony, $"Handling view change {viewChangeMessage.ViewChange} detected by {initiator.ID}");
                //Assume no view change while changing view
                if(_viewChangeInProgress == null)
                {
                    _viewChangeInProgress = new TaskCompletionSource<bool>();
                    _pendingViewChange = viewChangeMessage;

                    if(_pendingViewChange.ViewChange == ViewChangeMessage.ViewChangeOperation.Left)
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
                    
                    if(viewChangeMessage.ViewChange == ViewChangeMessage.ViewChangeOperation.Joined)
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
                            _logger.Log(Tag.VirtualSynchrony, $"I can send my flush message for pending view change {_pendingViewChange.ViewChange} of {_pendingViewChange.Node}!");
                            _communicationManager.SendMulticast(new FlushMessage(_pendingViewChange.Node, _pendingViewChange.ViewChange));
                            _flushed = true;
                        }

                        if(_flushedNodes.SetEquals(_pendingViewChange.ViewChange == ViewChangeMessage.ViewChangeOperation.Left ? _newGroupView : View.Others))
                        {
                            _logger.Log(Tag.VirtualSynchrony, $"All nodes have flushed their messages, consolidating view change");
                            //Enstablish new View
                            View.Update(_newGroupView);
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
                                _communicationManager.Send(_currentJoinRequest.JoiningNode, new ViewSyncResponse(View.Others.ToList(), _messageTimeStamper));
                                _currentJoinRequest = null;
                            }

                            //Unlock group communication
                            viewChangeTask.SetResult(true);
                        }
                    }
                }
            }
        }

        private async void OnJoinRequestReceived(Node node, Message message)
        {
            ViewJoinRequest joinRequest = message as ViewJoinRequest;

            await CheckViewChanges();

            joinRequest.BindToRegistry(_nodeRegistry);
            lock(View)
            {
                if(!View.Others.Contains(joinRequest.JoiningNode) && _currentJoinRequest == null)
                {
                    if(View.Me == View.Coordinator)
                    {
                        _logger.Log(Tag.VirtualSynchrony, $"Starting joining procedure");
                        //Only the coordinator processes these
                        _currentJoinRequest = joinRequest;
                        HandleViewChange(View.Me, new ViewChangeMessage(joinRequest.JoiningNode, ViewChangeMessage.ViewChangeOperation.Joined, _messageTimeStamper));
                    }
                }
            }
        }

        private void OnViewSyncReceived(Node coordinator, Message message)
        {
            lock(View)
            {
                if(_joinRequestCompletion != null && !_joinRequestCompletion.Task.IsCanceled)
                {
                    ViewSyncResponse viewSyncResponse = message as ViewSyncResponse;
                    viewSyncResponse.BindToRegistry(_nodeRegistry);

                    HashSet<Node> newView = viewSyncResponse.ViewNodes.ToHashSet();
                    
                    if(!newView.Contains(View.Me))
                        _logger.Fatal(Tag.VirtualSynchrony, "Received a new view where I'm not included!", new Exception("Received a new view where I'm not included!"));
                    newView.Remove(View.Me);
                    newView.Add(coordinator);

                    View.Update(newView);
                    View.UpdateCoordinator(coordinator);
                    _logger.Log(Tag.VirtualSynchrony, $"Received view sync from coordinator {View.Coordinator}{Environment.NewLine} Synched view: {View.Others.ToString<Node>()}");

                    _joinRequestCompletion.SetResult(true);
                }
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

            _logger.Log(Tag.VirtualSynchrony, "Registered for VS messages");

            //Start group join if we are not in a Group
            if(View.Coordinator == null)
            {
                _logger.Log(Tag.VirtualSynchrony, "No coordinator detected, trying to join existing group...");
                do
                {
                    _joinRequestCompletion = new TaskCompletionSource<bool>();
                    await _communicationManager.SendMulticast(new ViewJoinRequest(View.Me, _messageTimeStamper));
                    await Task.WhenAny(
                        _joinRequestCompletion.Task,
                        Task.Delay(5000 + (new Random().Next(5000))) //5 seconds timeout + (0,5) random seconds
                    );

                    if(!_joinRequestCompletion.Task.IsCompleted)
                    {
                        _joinRequestCompletion.SetCanceled();
                        _logger.Warning(Tag.VirtualSynchrony, "View join request timedout, trying again...");
                    }
                    else
                    {
                        _logger.Log(Tag.VirtualSynchrony, "Finished startup sequence!");
                        _joinRequestCompletion = null;
                    }
                } while(_joinRequestCompletion != null);
            }
            else
                _logger.Log(Tag.VirtualSynchrony, "Coordinator detected, finished startup sequence.");
        }

        public void Stop()
        {
            _logger.Log(Tag.VirtualSynchrony, "Stopping node, sending view change message");
            //TODO: A node advertising he is leaving, might want to double check if we assumed it is possible.
            _joinRequestCompletion = null;
            HandleViewChange(View.Me, new ViewChangeMessage(View.Me, ViewChangeMessage.ViewChangeOperation.Left, _messageTimeStamper));
        }
    }
}