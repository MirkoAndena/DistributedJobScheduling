using System;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Tests.Communication;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit;

namespace DistributedJobScheduling.Tests
{
    public class VirtualSynchronyTest
    {
        [Fact]
        public void VirtualSynchronyJoin()
        {
            StubNetworkBus networkBus = new StubNetworkBus();

            var node1 = StartUpNode(0, true, networkBus);
            var node2 = StartUpNode(2, false, networkBus);
            var node3 = StartUpNode(3, false, networkBus);
        }

        private (Node, ICommunicationManager, IGroupViewManager) StartUpNode(int id, bool coordinator, StubNetworkBus networkBus)
        {
            Node node = new Node($"127.0.0.{id}", id, coordinator);
            ICommunicationManager commMgr = null;
            IGroupViewManager groupManager = new GroupViewManager(commMgr);

            return (node, commMgr, groupManager);
        }
    }
}
