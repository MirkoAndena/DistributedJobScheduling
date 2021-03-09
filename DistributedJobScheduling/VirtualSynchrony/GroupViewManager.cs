using System.Collections.Generic;
namespace DistributedJobScheduling.VirtualSynchrony
{
    using System;
    using System.Threading.Tasks;
    using DependencyInjection;
    using Communication.Basic;
    using Communication;
    using DistributedJobScheduling.Communication.Topics;
    using DistributedJobScheduling.Communication.Messaging;

    //TODO: Timeouts?
    public class GroupViewManager : IGroupViewManager
    {
        private class MulticastNotDeliveredException : Exception {}

        public ITopicOutlet Topics { get; private set; }
        public Node Me => null;
        public HashSet<Node> GroupView { get; private set; } 

        private ICommunicationManager _communicationManager;
        private ITimeStamper _messageTimeStamper;
        private ITopicPublisher _virtualSynchronyTopic;

        /// <summary>
        /// Maps the tuple (senderID, timestamp) to the hashset of nodes that need to acknowledge the message
        /// (Receiving the message is a type of aknowledgment, acks might arrive before the message)
        /// </summary>
        private Dictionary<(int,int), HashSet<Node>> _confirmationMap;
        private Dictionary<(int,int), TemporaryMessage> _confirmationQueue;
        private HashSet<TemporaryMessage> _sentTemporaryMessages;
        private Dictionary<TemporaryMessage, TaskCompletionSource<bool>> _sendComplenentionMap;

        public GroupViewManager() : this(DependencyManager.Get<ICommunicationManager>(), DependencyManager.Get<ITimeStamper>()) {}
        internal GroupViewManager(ICommunicationManager communicationManager, ITimeStamper timeStamper)
        {
            _communicationManager = communicationManager;
            _messageTimeStamper = timeStamper;
            _confirmationMap = new Dictionary<(int, int), HashSet<Node>>();
            _confirmationQueue = new Dictionary<(int, int), TemporaryMessage>();
            _sentTemporaryMessages = new HashSet<TemporaryMessage>();
            _sendComplenentionMap = new Dictionary<TemporaryMessage, TaskCompletionSource<bool>>();

            _virtualSynchronyTopic = _communicationManager.Topics.GetPublisher<VirtualSynchronyTopicPublisher>();
            Topics = new GenericTopicOutlet(this);
            GroupView = new HashSet<Node>();

            _virtualSynchronyTopic.RegisterForMessage(typeof(TemporaryMessage), OnTemporaryMessageReceived);
            _virtualSynchronyTopic.RegisterForMessage(typeof(TemporaryAckMessage), OnTemporaryAckReceived);
        }

        public event Action<Node, Message> OnMessageReceived;

        public Task Send(Node node, Message message, int timeout = 30)
        {
            throw new NotImplementedException();
        }

        public Task<T> SendAndWait<T>(Node node, Message message, int timeout = 30) where T : Message
        {
            throw new NotImplementedException();
        }

        public async Task SendMulticast(Message message)
        {
            TemporaryMessage tempMessage = new TemporaryMessage(message);
            var messageKey = (Me.ID.Value, tempMessage.TimeStamp);
            _confirmationQueue.Add(messageKey, tempMessage);
            _sentTemporaryMessages.Add(tempMessage);
            _sendComplenentionMap.Add(tempMessage, new TaskCompletionSource<bool>(false));

            await _communicationManager.SendMulticast(tempMessage);

            ProcessAcknowledge(messageKey, Me);

            //True if every node in the view acknowledged the message
            if(!await _sendComplenentionMap[tempMessage].Task)
                throw new MulticastNotDeliveredException();
        }

        private void OnTemporaryMessageReceived(Node node, Message message)
        {
            TemporaryMessage tempMessage = message as TemporaryMessage;

            //Only care about messages from nodes in my current group view
            if(GroupView.Contains(node))
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
            if(GroupView.Contains(node))
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
                _confirmationMap.Add(messageKey, new HashSet<Node>(GroupView));
            
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
            
            //If sender, notify events
            if(_sentTemporaryMessages.Contains(message))
            {
                _sentTemporaryMessages.Remove(message);
                _sendComplenentionMap[message].SetResult(true);
                _sendComplenentionMap.Remove(message);
            }
            else
            {
                //If receiver notify reception
                //FIXME: Get node instance
                Node sender = null;
                OnMessageReceived?.Invoke(sender, message.UnstablePayload);
            }
        }
    }
}