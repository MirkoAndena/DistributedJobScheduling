using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication
{
    public interface ITopicPublisher
    {
        HashSet<Type> TopicMessageTypes { get; }
        event Action<Node, Message> OnMessagePublished;

        // messageType is passed as arguement to only compute its type once
        void RouteMessage(Type messageType, Node node, Message message);
        void RegisterForMessage(Type messageType, Action<Node, Message> onMessageReceived);
        void UnregisterForMessage(Type messageType, Action<Node, Message> onMessageReceived);
    }
}