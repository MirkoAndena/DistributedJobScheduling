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
using System.Linq;

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
            StubNetworkBus networkBus = new StubNetworkBus(new Random().Next());//123); //3 before 2
            networkBus.LatencyDeviation = 0.1f;
            FakeNode[] nodes = new FakeNode[10];
            int joinTimeout = 100; //ms
            int messageCount = 100;

            for(int i = 0; i < nodes.Length; i++)
                nodes[i] = StartUpNode(i, i == 0, networkBus, joinTimeout);

            var messages = new Dictionary<int, Dictionary<int, List<Message>>>();
            var receiveTask = Task.WhenAll(SetupMulticastAwaiters(messages, messageCount, nodes));
            
            Parallel.ForEach(nodes.AsParallel(), async node => {
                for(int i = 0; i < messageCount; i++)
                {
                    Message message = new EmptyMessage(node.timeStamper);
                    await node.commMgr.SendMulticast(message);
                }
            });
            
            await Task.Run(async () =>
            {
                await receiveTask;
                AssertMulticasts(messages, messageCount);
            });
        }

        private Task[] SetupMulticastAwaiters(Dictionary<int, Dictionary<int, List<Message>>> messages, int expectedCount, params FakeNode[] nodes)
        {
            List<Task> waitForNodes = new List<Task>();
            nodes.ForEach(x => {
                messages.Add(x.node.ID.Value, new Dictionary<int, List<Message>>());
                nodes.Where(y => y.node.ID != x.node.ID).ForEach(y => {
                    messages[x.node.ID.Value].Add(y.node.ID.Value, new List<Message>());
                });
            });

            nodes.ForEach(node => {
                TaskCompletionSource<bool> waitForNodeViewChange = new TaskCompletionSource<bool>();
                
                node.commMgr.OnMessageReceived += (Node sender, Message msg) => {
                    messages[node.node.ID.Value][sender.ID.Value].Add(msg);
                    
                    var myMessages = messages[node.node.ID.Value];
                    foreach(var messageList in myMessages.Values)
                        if(messageList.Count != expectedCount)
                            return;
                    
                    waitForNodeViewChange.SetResult(true);
                };

                waitForNodes.Add(waitForNodeViewChange.Task);
            });

            return waitForNodes.ToArray();
        }

        private void AssertMulticasts(Dictionary<int, Dictionary<int, List<Message>>> messages, int expectedCount)
        {
            //Assert FIFO Links
            foreach(var link in messages.Values)
                foreach(var sequence in link.Values)
                    for(int i = 0; i < expectedCount - 1; i++)
                        Assert.True(sequence[i].TimeStamp < sequence[i+1].TimeStamp);
        }

        [Fact]
        public async Task SimpleJoin()
        {
            StubNetworkBus networkBus = new StubNetworkBus(new Random().Next());//123); //3 before 2
            networkBus.LatencyDeviation = 0;
            FakeNode[] nodes = new FakeNode[10];
            int joinTimeout = 1000; //ms

            for(int i = 0; i < nodes.Length; i++)
                nodes[i] = StartUpNode(i, i == 0, networkBus, joinTimeout);
            
            for(int i = 0; i < nodes.Length; i++)
            {
                nodes[i].Start();
                await Task.Delay(100);
            }

            await Task.Run(async () =>
            {
                await Task.WhenAny(Task.WhenAll(SetupGroupJoinAwaiters(nodes[0], nodes[1..])), 
                                   Task.Delay(nodes.Length * joinTimeout * 2)); //Worst Case delay
                AssertGroupJoinView(nodes);
            });
        }

        private Task[] SetupGroupJoinAwaiters(FakeNode coordinator, params FakeNode[] nodes)
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

        private void AssertGroupJoinView(params FakeNode[] nodes)
        {
            Node oneCoordinator = nodes[0].groupManager.View.Coordinator;
            foreach(FakeNode node in nodes)
            {
                Assert.True(node.groupManager.View != null);
                Assert.True(node.groupManager.View.Others.Count == nodes.Length - 1);
                Assert.True(node.groupManager.View.Coordinator == node.nodeRegistry.GetOrCreate(oneCoordinator));

                foreach(FakeNode otherNode in nodes)
                    if(otherNode != node)
                        Assert.True(node.groupManager.View.Others.Contains(node.nodeRegistry.GetOrCreate(otherNode.node)));
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

        private FakeNode StartUpNode(int id, bool coordinator, StubNetworkBus networkBus, int joinTimeout = 5000)
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
            ((GroupViewManager)groupManager).JoinRequestTimeout = joinTimeout;
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
