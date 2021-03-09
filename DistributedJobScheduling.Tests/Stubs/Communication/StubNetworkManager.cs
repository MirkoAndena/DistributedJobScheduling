using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Topics;

namespace DistributedJobScheduling.Tests.Communication
{
    public class StubNetworkManager : ICommunicationManager
    {
        public event Action<Node, Message> OnMessageReceived;
        private StubNetworkBus _networkBus;
        private Node _me;

        public ITopicOutlet Topics { get; private set; } = new GenericTopicOutlet(
            new VirtualSynchronyTopicPublisher()
        );

        public StubNetworkManager(Node node)
        {
            _me = node;
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
        }
    }
}