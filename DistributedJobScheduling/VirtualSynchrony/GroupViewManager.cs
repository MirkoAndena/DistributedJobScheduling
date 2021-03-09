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

    public class GroupViewManager : IGroupViewManager
    {
        public ITopicOutlet Topics { get; private set; }
        public HashSet<Node> GroupView { get; private set; }

        private ICommunicationManager _communicationManager;
        private ITopicPublisher _virtualSynchronyTopic;

        /// <summary>
        /// Maps the tuple (senderID, timestamp) to the hashset of nodes that need to acknowledge the message
        /// (Receiving the message is a type of aknowledgment, acks might arrive before the message)
        /// </summary>
        private Dictionary<(int,int), HashSet<Node>> _confirmationMap;
        private Dictionary<(int,int), TemporaryMessage> _confirmationQueue;

        public GroupViewManager() : this(DependencyManager.Get<ICommunicationManager>()) {}
        internal GroupViewManager(ICommunicationManager communicationManager)
        {
            _communicationManager = communicationManager;
            _confirmationMap = new Dictionary<(int, int), HashSet<Node>>();
            _confirmationQueue = new Dictionary<(int, int), TemporaryMessage>();
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

        public Task SendMulticast(Message message)
        {
            throw new NotImplementedException();
        }

        private void OnTemporaryMessageReceived(Node node, Message message)
        {
            TemporaryMessage tempMessage = message as TemporaryMessage;

            //Only care about messages from nodes in my current group view
            if(GroupView.Contains(node))
            {
                //SendMulticast(new TemporaryAckMessage())
            }
        }

        private void OnTemporaryAckReceived(Node node, Message message)
        {
            TemporaryAckMessage tempAckMessage = message as TemporaryAckMessage;
            
            //Only care about messages from nodes in my current group view
            if(GroupView.Contains(node))
            {
                var messageKey = (tempAckMessage.OriginalSenderID, tempAckMessage.OriginalTimestamp);
                
                if(!_confirmationMap.ContainsKey(messageKey))
                    _confirmationMap.Add(messageKey, new HashSet<Node>(GroupView));
                
                var confirmationSet = _confirmationMap[messageKey];
                confirmationSet.Remove(node);

                //TODO: Timeout here?

                if(confirmationSet.Count == 0)
                    ConsolidateTemporaryMessage(messageKey);
            }
        }

        private void ConsolidateTemporaryMessage((int,int) messageKey)
        {

        }
    }
}