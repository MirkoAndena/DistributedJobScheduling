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

namespace DistributedJobScheduling.VirtualSynchrony
{

    //TODO: Timeouts?
    public class GroupViewManager : IGroupViewManager
    {
        private class MulticastNotDeliveredException : Exception {}

        public Group View { get; private set; }

        public ITopicOutlet Topics { get; private set; }

        private ICommunicationManager _communicationManager;
        private ITimeStamper _messageTimeStamper;
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

        private ViewChangeMessage _pendingViewChange;
        private TaskCompletionSource<bool> _viewChangeInProgress;
        private HashSet<Node> _newGroupView;
        private HashSet<Node> _flushedNodes;
        private bool _flushed;

        #endregion

        public GroupViewManager() : this(DependencyManager.Get<Node.INodeRegistry>(),
                                         DependencyManager.Get<ICommunicationManager>(), 
                                         DependencyManager.Get<ITimeStamper>(),
                                         DependencyManager.Get<IConfigurationService>()
                                         ) {}
        internal GroupViewManager(Node.INodeRegistry nodeRegistry,
                                  ICommunicationManager communicationManager, 
                                  ITimeStamper timeStamper,
                                  IConfigurationService configurationService)
        {
            _communicationManager = communicationManager;
            _messageTimeStamper = timeStamper;
            _confirmationMap = new Dictionary<(int, int), HashSet<Node>>();
            _confirmationQueue = new Dictionary<(int, int), TemporaryMessage>();
            _sentTemporaryMessages = new HashSet<TemporaryMessage>();
            _sendComplenentionMap = new Dictionary<TemporaryMessage, TaskCompletionSource<bool>>();

            _virtualSynchronyTopic = _communicationManager.Topics.GetPublisher<VirtualSynchronyTopicPublisher>();
            Topics = new GenericTopicOutlet(this);
            View = new Group(_nodeRegistry.GetOrCreate(id: configurationService.GetValue<int?>("nodeId", null)), false);

            _virtualSynchronyTopic.RegisterForMessage(typeof(TemporaryMessage), OnTemporaryMessageReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(TemporaryAckMessage), OnTemporaryAckReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(ViewChangeMessage), OnTemporaryAckReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(FlushMessage), OnTemporaryAckReceived);
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
            throw new NotImplementedException();
        }

        public async Task<T> SendAndWait<T>(Node node, Message message, int timeout = 30) where T : Message
        {
            await CheckViewChanges();
            throw new NotImplementedException();
        }

        public async Task SendMulticast(Message message)
        {
            await CheckViewChanges();

            TemporaryMessage tempMessage = new TemporaryMessage(message);
            var messageKey = (View.Me.ID.Value, tempMessage.TimeStamp);
            _confirmationQueue.Add(messageKey, tempMessage);
            _sentTemporaryMessages.Add(tempMessage);
            _sendComplenentionMap.Add(tempMessage, new TaskCompletionSource<bool>(false));

            await _communicationManager.SendMulticast(tempMessage);

            ProcessAcknowledge(messageKey, View.Me);

            //True if every node in the view acknowledged the message
            if(!await _sendComplenentionMap[tempMessage].Task)
                throw new MulticastNotDeliveredException();
        }

        private void OnTemporaryMessageReceived(Node node, Message message)
        {
            TemporaryMessage tempMessage = message as TemporaryMessage;

            //Only care about messages from nodes in my current group view
            if(View.Others.Contains(node))
            {
                var messageKey = (node.ID.Value, tempMessage.TimeStamp);
                _confirmationQueue.Add(messageKey, tempMessage);
                _communicationManager.SendMulticast(new TemporaryAckMessage(tempMessage, _messageTimeStamper));

                ProcessAcknowledge(messageKey, node);
            }
        }

        private void OnTemporaryAckReceived(Node node, Message message)
        {
            TemporaryAckMessage tempAckMessage = message as TemporaryAckMessage;
            
            //Only care about messages from nodes in my current group view
            if(View.Others.Contains(node))
            {
                var messageKey = (tempAckMessage.OriginalSenderID, tempAckMessage.OriginalTimestamp);
                ProcessAcknowledge(messageKey, node);   
            }
        }

        ///<summary>
        ///Updates the confirmation map, resets timeouts and consolidates messages that revceived every ack
        ///</summary>
        private void ProcessAcknowledge((int,int) messageKey, Node node)
        {
            if(!_confirmationMap.ContainsKey(messageKey))
                _confirmationMap.Add(messageKey, new HashSet<Node>(View.Others));
            
            var confirmationSet = _confirmationMap[messageKey];
            confirmationSet.Remove(node);

            //TODO: Reset timeout here?

            if(confirmationSet.Count == 0)
                ConsolidateTemporaryMessage(messageKey);
        }

        ///<summary>
        ///Cleans up the state of the hash sets and notifies events
        ///</summary>
        private void ConsolidateTemporaryMessage((int,int) messageKey)
        {
            var message = _confirmationQueue[messageKey];
            _confirmationMap.Remove(messageKey);
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

        private void OnFlushMessageReceived(Node node, Message message)
        {
            var flushMessage = message as FlushMessage;
            if(View.Others.Contains(node))
            {
                //Flush messages can arrive before anyone notified this node about the change
                if(_viewChangeInProgress == null)
                    HandleViewChange(View.Me, new ViewChangeMessage(flushMessage.RelatedChangeNode, flushMessage.RelatedChangeOperation)); //Self-Report Viewchange

                _flushedNodes.Add(node);

                HandleFlushCondition();
            }
        }

        private void OnViewChangeReceived(Node node, Message message)
        {
            var viewChangeMessage = message as ViewChangeMessage;
            if(View.Others.Contains(node))
                HandleViewChange(node, viewChangeMessage);
        }

        /// <summary>
        /// Handles a View change notified by someone
        /// </summary>
        /// <param name="viewChangeMessage">Message containing the view change</param>
        private void HandleViewChange(Node initiator, ViewChangeMessage viewChangeMessage)
        {
            Node viewChange = _nodeRegistry.GetOrCreate(viewChangeMessage.Node);
            //Assume no view change while changing view
            if(_viewChangeInProgress == null)
            {
                _viewChangeInProgress = new TaskCompletionSource<bool>();
                _pendingViewChange = viewChangeMessage;

                if(_pendingViewChange.ViewChange == ViewChangeMessage.ViewChangeOperation.Left)
                {
                    //FIXME: Shouldn't we multicast unstable messages from the dead node? Probably handled by timeout (either all or none)
                    //FIXME: Doesn't this potentially lead to the consolidation of messages we don't have? (everyone ACKed the message but the crashed node still didn't send it to us)
                    //Ignore acknowledges from the dead node
                    _confirmationMap.Keys.ForEach(messageKey => ProcessAcknowledge(messageKey, viewChange));
                }

                //Setup Message Flushing and check if we can already flush
                _flushed = false;
                _flushedNodes = new HashSet<Node>();
                _newGroupView = new HashSet<Node>(View.Others);
                
                if(viewChangeMessage.ViewChange == ViewChangeMessage.ViewChangeOperation.Joined)
                    _newGroupView.Add(viewChange);
                else
                    _newGroupView.Remove(viewChange);

                HandleFlushCondition(); 
            }
            else if((initiator == View.Me || initiator != viewChange) && !_pendingViewChange.IsSame(viewChangeMessage))
            {
                //If we got a new viewchange that isn't the same as the one already received
                //FATAL: Double View Change
                _viewChangeInProgress.SetResult(false);
                throw new Exception("FATAL: ViewChange during view change");
            }
        }

        //If we are not waiting any more messages from alive nodes we need to consolidate messages
        private bool FlushCondition() => _pendingViewChange != null && !_confirmationMap.Any(pair => { return !pair.Value.IsSubsetOf(_flushedNodes); });
        private void HandleFlushCondition()
        {
            if(FlushCondition())
            {
                if(!_flushed)
                {
                    _communicationManager.SendMulticast(new FlushMessage(_pendingViewChange.Node, _pendingViewChange.ViewChange));
                    _flushed = true;
                }

                if(_flushedNodes.SetEquals(_newGroupView))
                {
                    //Enstablish new View
                    View.Update(_newGroupView);
                    var viewChangeTask = _viewChangeInProgress;

                    //Reset view state change
                    _flushed = false;
                    _flushedNodes = null;
                    _pendingViewChange = null;
                    _viewChangeInProgress = null;
                    _newGroupView = null;

                    //Unlock group communication
                    viewChangeTask.SetResult(true);
                }
            }
        }
    }
}