using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;

namespace DistributedJobScheduling.Communication.Topics
{
    /// <summary>
    /// Dictionary and Delegate concatenation based TopicPublisher
    /// </summary>
    public abstract class BaseTopicPublisher : ITopicPublisher
    {
        public event Action<Node, Message> OnMessagePublished;

        private Dictionary<Type, Action<Node, Message>> _messageRegistrations;

        public abstract HashSet<Type> TopicMessageTypes { get; }

        public BaseTopicPublisher()
        {
            _messageRegistrations = new Dictionary<Type, Action<Node, Message>>();
        }

        public void RouteMessage(Type messageType, Node node, Message message) 
        {
            if(TopicMessageTypes.Contains(messageType))
            {
                OnMessagePublished?.Invoke(node, message);
                if(_messageRegistrations.ContainsKey(messageType))
                    _messageRegistrations[messageType]?.Invoke(node, message);
            }
        }

        public void RegisterForMessage(Type messageType, Action<Node, Message> onMessageReceived) 
        {
            if(TopicMessageTypes.Contains(messageType))
            {
                if(!_messageRegistrations.ContainsKey(messageType))
                    _messageRegistrations.Add(messageType, onMessageReceived);
                else
                    _messageRegistrations[messageType] += onMessageReceived;
            }
        }

        public void UnregisterForMessage(Type messageType, Action<Node, Message> onMessageReceived) 
        {
            if(_messageRegistrations.ContainsKey(messageType))
                _messageRegistrations[messageType] -= onMessageReceived;
        }
    }
}