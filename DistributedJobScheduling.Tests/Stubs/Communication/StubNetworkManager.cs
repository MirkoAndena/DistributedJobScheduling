using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Tests.Communication
{
    public class StubNetworkManager : ICommunicationManager
    {
        public event Action<Node, Message> OnMessageReceived;
        private Dictionary<Type, ITopicPublisher> _topics;
        private StubNetworkBus _networkBus;
        private Node _me;

        public StubNetworkManager(Node node)
        {
            _topics = new Dictionary<Type, ITopicPublisher>();
            _me = node;
        }

        public ITopicPublisher GetPublisher(Type topicType)
        {
            if(_topics.ContainsKey(topicType))
                return _topics[topicType];
            return null;
        }

        public async Task Send(Node node, Message message, int timeout = 30)
        {
            if(_networkBus == null)
                throw new Exception("Connection failure!");

            await _networkBus.SendTo(_me, node, message, timeout);
        }

        public Task<T> SendAndWait<T>(Node node, Message message, int timeout = 30) where T : Message
        {
            if(_networkBus == null)
                throw new Exception("Connection failure!");
            throw new NotImplementedException();
        }

        public async Task SendMulticast(Message message)
        {
            if(_networkBus == null)
                throw new Exception("Connection failure!");

            await _networkBus.SendMulticast(_me, message);
        }

        //Test methods
        public void RegisteredToNetwork(StubNetworkBus networkBus)
        {
            _networkBus = networkBus;
        }

        public void FakeReceive(Node node, Message message)
        {
            OnMessageReceived?.Invoke(node, message);
            _topics.Values.ForEach(t => t.RouteMessage(node, message));
        }
    }
}