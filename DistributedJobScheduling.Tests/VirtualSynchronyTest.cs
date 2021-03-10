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

namespace DistributedJobScheduling.Tests
{
    public class VirtualSynchronyTest
    {
        public class EmptyMessage : Message 
        {
            public EmptyMessage(ITimeStamper timeStamper) : base(timeStamper) {}
        }
        
        [Fact]
        public async void VirtualSynchronyJoin()
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

        private struct FakeNode
        {
            public Node node;
            public ICommunicationManager commMgr;
            public IGroupViewManager groupManager;
            public ITimeStamper timeStamper;
        }

        private FakeNode StartUpNode(int id, bool coordinator, StubNetworkBus networkBus)
        {
            Node.INodeRegistry nodeRegistry = new Node.NodeRegistryService();
            Node node = nodeRegistry.GetOrCreate($"127.0.0.{id}", id);
            StubNetworkManager commMgr = new StubNetworkManager(node);
            ITimeStamper nodeTimeStamper = new StubScalarTimeStamper(node);
            IGroupViewManager groupManager = new GroupViewManager(nodeRegistry,
                                                                  commMgr, 
                                                                  nodeTimeStamper, 
                                                                  new FakeConfigurator(new Dictionary<string, object> {
                                                                    ["nodeId"] = id
                                                                }));

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
