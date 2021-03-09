using System;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Tests.Communication;
using DistributedJobScheduling.Tests.Communication.Messaging;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit;

namespace DistributedJobScheduling.Tests
{
    public class VirtualSynchronyTest
    {
        public class EmptyMessage : Message 
        {
            public EmptyMessage(ITimeStamper timeStamper) : base(timeStamper) {}
        }
        
        [Fact]
        public void VirtualSynchronyJoin()
        {
            StubNetworkBus networkBus = new StubNetworkBus(123);

            var node1 = StartUpNode(0, true, networkBus);
            var node2 = StartUpNode(2, false, networkBus);
            var node3 = StartUpNode(3, false, networkBus);

            bool _wasReceivedBy2 = false, _wasReceivedBy3 = false;
            node2.commMgr.OnMessageReceived += (n,m) => _wasReceivedBy2 = true;
            node3.commMgr.OnMessageReceived += (n,m) => _wasReceivedBy3 = true;

            node1.commMgr.SendMulticast(new EmptyMessage(node1.timeStamper)).Wait();

            Assert.True(_wasReceivedBy2 && _wasReceivedBy3);
        }

        private struct FakeNode
        {
            public Node node;
            public ICommunicationManager commMgr;
            public IGroupViewManager groupManager;
            public ITimeStamper timeStamper;
        }

        private FakeNode StartUpNode(int id, bool coordinator, StubNetworkBus networkBus)
        {
            Node node = new Node($"127.0.0.{id}", id);
            StubNetworkManager commMgr = new StubNetworkManager(node);
            ITimeStamper nodeTimeStamper = new StubScalarTimeStamper(node);
            IGroupViewManager groupManager = new GroupViewManager(commMgr, nodeTimeStamper);

            networkBus.RegisterToNetwork(node, commMgr);

            return new FakeNode (){
                node = node, 
                commMgr = commMgr, 
                groupManager = groupManager, 
                timeStamper = nodeTimeStamper
            };
        }
    }
}
