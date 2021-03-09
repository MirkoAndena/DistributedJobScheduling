using System;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;

///<summary>
///Manager that abstracts TCP communication between the executors
///</summary>

namespace DistributedJobScheduling.Communication
{
    public interface ICommunicationManager
    {
        ITopicOutlet Topics { get; }
        event Action<Node, Message> OnMessageReceived;
        Task<T> SendAndWait<T>(Node node, Message message, int timeout = 30) where T: Message;
        Task Send(Node node, Message message, int timeout = 30);
        Task SendMulticast(Message message);
    }
}