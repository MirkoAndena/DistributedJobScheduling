namespace DistributedJobScheduling.VirtualSynchrony
{
    using System;
    using System.Threading.Tasks;
    using DependencyInjection;
    using Communication.Basic;
    using Communication;

    public class GroupViewManager : IGroupViewManager
    {
        private ICommunicationManager _communicationManager;
        public GroupViewManager() : this(DependencyManager.Get<ICommunicationManager>()) {}
        internal GroupViewManager(ICommunicationManager communicationManager)
        {
            _communicationManager = communicationManager;
        }

        public event Action<Node, Message> OnMessageReceived;

        public ITopicPublisher GetPublisher(Type topicType)
        {
            throw new NotImplementedException();
        }

        public Task Send(Node node, Message message, int timeout = 30)
        {
            throw new NotImplementedException();
        }

        public Task<T> SendAndWait<T>(Node node, Message message, int timeout = 30) where T : Message
        {
            throw new NotImplementedException();
        }

        public Task SendMulticast(Message message)
        {
            throw new NotImplementedException();
        }
    }
}