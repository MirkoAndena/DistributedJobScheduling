using System;
using System.Threading.Tasks;
using Communication;
///<summary>
///Manager that abstracts TCP communication between the executors
///</summary>
public interface ICommunicationManager
{
    event Action<Node, Message> OnMessageReceived;
    Task<T> SendAndWait<T>(Node node, Message message, int timeout = 30) where T: Message;
    Task Send(Node node, Message message, int timeout = 30);
    Task SendMulticast(Message message);
    ITopicPublisher GetPublisher(Type topicType);
}