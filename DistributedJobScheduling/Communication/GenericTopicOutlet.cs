using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication
{
    /// <summary>
    /// Facade to handle topic subscriptions and parsing
    /// </summary>
    public class GenericTopicOutlet : ITopicOutlet
    {
        private Dictionary<Type, ITopicPublisher> _topics;

        public GenericTopicOutlet(params ITopicPublisher[] topics)
        {
            _topics = new Dictionary<Type, ITopicPublisher>();
            topics.ForEach(RegisterPublisher);
        }

        public void RegisterPublisher<T>(T publisher) where T : ITopicPublisher
        {
            Type publisherType = typeof(T);
            if(!_topics.ContainsKey(publisherType))
                _topics.Add(publisherType, publisher);
        }

        public ITopicPublisher GetPublisher<T>() where T : ITopicPublisher
        {
            Type publisherType = typeof(T);
            if(_topics.ContainsKey(publisherType))
                return _topics[publisherType];
            return null;
        }

        public void PublishMessage(Node node, Message message)
        {
            Type messageType = message.GetType();
            _topics.Values.ForEach(t => t.RouteMessage(messageType, node, message));
        }
    }
}