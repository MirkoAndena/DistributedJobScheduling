using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Communication
{
    /// <summary>
    /// Facade to handle topic subscriptions and parsing
    /// </summary>
    public class GenericTopicOutlet : ITopicOutlet
    {
        private Dictionary<Type, ITopicPublisher> _topics;
        private ILogger _logger;

        public GenericTopicOutlet(ICommunicationManager communicationManger, ILogger logger, params ITopicPublisher[] topics)
        {
            _logger = logger;
            _topics = new Dictionary<Type, ITopicPublisher>();
            topics.ForEach(RegisterPublisher);
            communicationManger.OnMessageReceived += PublishMessage;
        }

        public void RegisterPublisher<T>(T publisher) where T : ITopicPublisher
        {
            Type publisherType = publisher.GetType();
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
            try
            {
                Type messageType = message.GetType();
                _topics.Values.ForEach(t => t.RouteMessage(messageType, node, message));
            }
            catch(Exception ex)
            {
                _logger.Error(Tag.UnHandled, ex);
                throw;
            }
        }
    }
}