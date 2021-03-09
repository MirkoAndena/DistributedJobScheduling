using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication
{
    /// <summary>
    /// Facade to handle topic subscriptions and parsing
    /// </summary>
    public interface ITopicOutlet
    {
        ITopicPublisher GetPublisher<T>() where T : ITopicPublisher;
        void RegisterPublisher<T>(T publisher) where T : ITopicPublisher;
        void PublishMessage(Node node, Message message);
    }
}