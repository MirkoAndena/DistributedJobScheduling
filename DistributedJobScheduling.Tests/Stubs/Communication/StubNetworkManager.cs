using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.Ordering;
using DistributedJobScheduling.Communication.Topics;

namespace DistributedJobScheduling.Tests.Communication
{
    public class StubNetworkManager : ICommunicationManager
    {
        public event Action<Node, Message> OnMessageReceived;
        private StubNetworkBus _networkBus;
        private IMessageOrdering _sendOrdering;
        private int? _lastSent = null;
        private Dictionary<int, TaskCompletionSource<bool>> _waitingSendQueue;
        private Node _me;

        public ITopicOutlet Topics { get; private set; }

        public StubNetworkManager(Node node)
        {
            _me = node;

            _sendOrdering = new FIFOMessageOrdering();
            Topics = new GenericTopicOutlet(this,
                new VirtualSynchronyTopicPublisher()
            );
        }

        public async Task Send(Node node, Message message, int timeout = 30)
        {
            if(_networkBus == null)
                throw new Exception("Connection failure!");

            await _sendOrdering.EnsureOrdering(message);
            await _networkBus.SendTo(_me, node, message, timeout);
            _sendOrdering.Observe(message);
        }

        public async Task SendMulticast(Message message)
        {
            if(_networkBus == null)
                throw new Exception("Connection failure!");

            await _sendOrdering.EnsureOrdering(message);
            await _networkBus.SendMulticast(_me, message);
            _sendOrdering.Observe(message);
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