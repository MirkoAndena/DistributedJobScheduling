using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.Tests.Communication;
using DistributedJobScheduling.Tests.Communication.Messaging;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit;
using Xunit.Abstractions;

namespace DistributedJobScheduling.Tests
{
    public class VirtualSynchronyTest
    {
        private readonly ITestOutputHelper _output;
        public VirtualSynchronyTest(ITestOutputHelper output)
        {
            _output = output;
        }

        public class EmptyMessage : Message 
        {
            public EmptyMessage(ITimeStamper timeStamper) : base(timeStamper) {}
        }
        
        [Fact]
        public async Task MulticastWorks()
        {
            StubNetworkBus networkBus = new StubNetworkBus(123);

            var node1 = StartUpNode(0, true, networkBus);
            var node2 = StartUpNode(2, false, networkBus);
            var node3 = StartUpNode(3, false, networkBus);
            CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<bool>[] _received = new TaskCompletionSource<bool>[]
            {
                new TaskCompletionSource<bool>(),
                new TaskCompletionSource<bool>()
            };
            node2.commMgr.OnMessageReceived += (n,m) => _received[0].SetResult(true);
            node3.commMgr.OnMessageReceived += (n,m) => _received[1].SetResult(true);

            node1.commMgr.SendMulticast(new EmptyMessage(node1.timeStamper)).Wait();
            for(int i = 0; i < _received.Length; i++)
            {
                Task task = Task.Run(async () =>
                {
                    await Task.Delay(1000, cts.Token);
                    _received[i].SetResult(false);
                });
            }

            await Task.WhenAll(_received[0].Task, _received[1].Task);

            Assert.True(_received[0].Task.Result && _received[0].Task.Result);
            cts.Cancel();
        }

        [Fact]
        public async Task SimpleJoin()
        {
            StubNetworkBus networkBus = new StubNetworkBus(123);

            var node1 = StartUpNode(0, true, networkBus);
            var node2 = StartUpNode(2, false, networkBus);
            var node3 = StartUpNode(3, false, networkBus);
            
            node1.Start();
            Assert.True(node1.groupManager.View != null && node1.groupManager.View.Coordinator == node1.node);
            await Task.Delay(50);
            node2.Start();
            await Task.Delay(1000);
            Assert.True(node1.groupManager.View.Others.Count == 1);
            Assert.True(node1.groupManager.View.Coordinator == node1.node);
            Assert.True(node2.groupManager.View != null);
            Assert.True(node2.groupManager.View.Others.Count == 1);
            Assert.True(node2.groupManager.View.Coordinator == node2.nodeRegistry.GetOrCreate(node1.node));
            node3.Start();
            await Task.Delay(1000);
            Assert.True(node1.groupManager.View.Others.Count == 2);
            Assert.True(node1.groupManager.View.Coordinator == node1.node);
            Assert.True(node2.groupManager.View.Others.Count == 2);
            Assert.True(node2.groupManager.View.Coordinator == node2.nodeRegistry.GetOrCreate(node1.node));
            Assert.True(node3.groupManager.View != null);
            Assert.True(node3.groupManager.View.Others.Count == 2);
            Assert.True(node3.groupManager.View.Coordinator == node3.nodeRegistry.GetOrCreate(node1.node));
        }

        private class FakeNode
        {
            public Node node;
            public ICommunicationManager commMgr;
            public IGroupViewManager groupManager;
            public ITimeStamper timeStamper;
            public Node.INodeRegistry nodeRegistry;

            public void Start()
            {
                if(groupManager is GroupViewManager groupViewManager)
                    groupViewManager.Start();
            }
        }

        private FakeNode StartUpNode(int id, bool coordinator, StubNetworkBus networkBus)
        {
            Node.INodeRegistry nodeRegistry = new Node.NodeRegistryService();
            Node node = nodeRegistry.GetOrCreate($"127.0.0.{id}", id);
            StubLogger stubLogger = new StubLogger(node, _output);
            StubNetworkManager commMgr = new StubNetworkManager(node);
            ITimeStamper nodeTimeStamper = new StubScalarTimeStamper(node);
            IGroupViewManager groupManager = new GroupViewManager(nodeRegistry,
                                                                  commMgr, 
                                                                  nodeTimeStamper, 
                                                                  new FakeConfigurator(new Dictionary<string, object> {
                                                                    ["nodeId"] = id,
                                                                    ["coordinator"] = coordinator
                                                                  }),
                                                                  stubLogger);

            networkBus.RegisterToNetwork(node, commMgr);

            return new FakeNode (){
                node = node, 
                commMgr = commMgr, 
                groupManager = groupManager, 
                timeStamper = nodeTimeStamper,
                nodeRegistry = nodeRegistry
            };
        }
    }
}
