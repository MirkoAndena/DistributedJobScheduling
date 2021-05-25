using System;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using static DistributedJobScheduling.Communication.NetworkManager;

///<summary>
///Manager that abstracts TCP communication between the executors
///</summary>

namespace DistributedJobScheduling.Communication
{
    public interface ICommunicationManager
    {
        ITopicOutlet Topics { get; }
        event Action<Node, Message> OnMessageReceived;
        Task Send(Node node, Message message, SendFailureStrategy strategy = SendFailureStrategy.Discard);
        Task SendMulticast(Message message);
    }
}