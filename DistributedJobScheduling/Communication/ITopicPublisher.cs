using System;
using System.Collections.Generic;
using Communication;

public interface ITopicPublisher
{
    List<Type> TopicMessageTypes { get; }
    event Action<Node, Message> OnMessagePublished;
    void RouteMessage(Node node, Message message);
    void RegisterForMessage(Type messageType, Action<Node, Message> onMessageReceived);
    void UnregisterForMessage(Type messageType, Action<Node, Message> onMessageReceived);
}