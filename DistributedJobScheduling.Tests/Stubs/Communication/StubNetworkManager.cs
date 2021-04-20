using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.Ordering;
using DistributedJobScheduling.Communication.Topics;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Tests.Communication
{
    public class StubNetworkManager : ICommunicationManager
    {
        public event Action<Node, Message> OnMessageReceived;
        private StubNetworkBus _networkBus;
        private IMessageOrdering _sendOrdering;
        private Node _me;

        public ITopicOutlet Topics { get; private set; }

        public StubNetworkManager(Node node, ILogger logger)
        {
            _me = node;

            _sendOrdering = new FIFOMessageOrdering(logger);
            Topics = new GenericTopicOutlet(this, logger,
                new VirtualSynchronyTopicPublisher()
            );
        }

        public async Task Send(Node node, Message message, int timeout = 30)
        {
            if(_networkBus == null)
                throw new Exception("Connection failure!");
            message.SenderID = _me.ID;
            message.ReceiverID = node.ID;
            await _sendOrdering.OrderedExecute(message, () => _networkBus.SendTo(_me, node, message, timeout));
        }

        public async Task SendMulticast(Message message)
        {
            if(_networkBus == null)
                throw new Exception("Connection failure!");

            message.SenderID = _me.ID;
            await _sendOrdering.OrderedExecute(message, () => _networkBus.SendMulticast(_me, message));
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