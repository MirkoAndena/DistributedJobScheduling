using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Extensions;
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
            StubNetworkBus networkBus = new StubNetworkBus(new Random().Next());//123); //3 before 2
            networkBus.LatencyDeviation = 1;
            FakeNode[] nodes = new FakeNode[4];

            for(int i = 0; i < nodes.Length; i++)
                nodes[i] = StartUpNode(i, i == 0, networkBus);
            
            for(int i = 0; i < nodes.Length; i++)
            {
                nodes[i].Start();
                await Task.Delay(1);
            }

            await Task.Run(async () =>
            {
                await Task.WhenAny(Task.WhenAll(SetupAwaiters(nodes[0], nodes[1..])), 
                                   Task.Delay(10000)); //* (nodes.Length - 1)));
                AssertView(nodes);
            });
        }

        private Task[] SetupAwaiters(FakeNode coordinator, params FakeNode[] nodes)
        {
            List<Task> waitForNodes = new List<Task>();

            nodes.ForEach(node => {
                TaskCompletionSource<bool> waitForNodeViewChange = new TaskCompletionSource<bool>();
                
                node.groupManager.View.ViewChanged += () => {
                    if(node.groupManager.View.Others.Count == nodes.Length && node.groupManager.View.Coordinator == node.nodeRegistry.GetOrCreate(coordinator.node))
                        waitForNodeViewChange.SetResult(true);
                };

                waitForNodes.Add(waitForNodeViewChange.Task);
            });

            return waitForNodes.ToArray();
        }

        private void AssertView(params FakeNode[] nodes)
        {
            Node oneCoordinator = nodes[0].groupManager.View.Coordinator;
            foreach(FakeNode node in nodes)
            {
                Assert.True(node.groupManager.View != null);
                Assert.True(node.groupManager.View.Others.Count == nodes.Length - 1);
                Assert.True(node.groupManager.View.Coordinator == node.nodeRegistry.GetOrCreate(oneCoordinator));

                /*foreach(FakeNode otherNode in nodes)
                    if(otherNode != node)
                        Assert.True(node.groupManager.View.Others.Contains(node.nodeRegistry.GetOrCreate(otherNode.node)));*/
            }
        }

        private class FakeNode
        {
            public Node node;
            public ICommunicationManager commMgr;
            public IGroupViewManager groupManager;
            public ITimeStamper timeStamper;
            public Node.INodeRegistry nodeRegistry;
            public StubLogger nodeLogger;

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
            groupManager.View.ViewChanged += () => { stubLogger.Log(Logging.Tag.VirtualSynchrony, $"View Changed: {groupManager.View.Others.ToString<Node>()}"); };
            networkBus.RegisterToNetwork(node, nodeRegistry, commMgr);

            return new FakeNode (){
                node = node, 
                commMgr = commMgr, 
                groupManager = groupManager, 
                timeStamper = nodeTimeStamper,
                nodeRegistry = nodeRegistry,
                nodeLogger = stubLogger
            };
        }
    }
}
