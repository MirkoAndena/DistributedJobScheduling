using System;
using System.Threading.Tasks;
using Communication;
///<summary>
///Manager that abstracts TCP communication between the executors
///</summary>
public interface ICommunicationManager
{

    event Action<Node, Message> OnMessageReceived;
    Task<Message> SendAndWait(Node node, Message message, int timeout = 30);
    Task Send(Node node, Message message, int timeout = 30);
    ITopicPublisher GetPublisher(Type topicType);
}