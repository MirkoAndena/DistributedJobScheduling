using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Tests.Communication;
using DistributedJobScheduling.Tests.Communication.Messaging;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using DistributedJobScheduling.Tests.Utils;

namespace DistributedJobScheduling.Tests
{
    public class VirtualSynchronyTest
    {
        private readonly ITestOutputHelper _output;
        public VirtualSynchronyTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task MulticastWorks()
        {
            StubNetworkBus networkBus = new StubNetworkBus(new Random().Next());//123); //3 before 2
            FakeNode[] nodes = new FakeNode[10];
            int joinTimeout = 100; //ms
            int messageCount = 100;

            for(int i = 0; i < nodes.Length; i++)
                nodes[i] = new FakeNode(i, i == 0, networkBus, _output, joinTimeout);

            var messages = new Dictionary<int, Dictionary<int, List<Message>>>();
            var receiveTask = Task.WhenAll(SetupMulticastAwaiters(messages, messageCount, nodes));
            
            Parallel.ForEach(nodes.AsParallel(), async node => {
                for(int i = 0; i < messageCount; i++)
                {
                    Message message = new EmptyMessage(node.TimeStamper);
                    await node.Communication.SendMulticast(message);
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
                messages.Add(x.Node.ID.Value, new Dictionary<int, List<Message>>());
                nodes.Where(y => y.Node.ID != x.Node.ID).ForEach(y => {
                    messages[x.Node.ID.Value].Add(y.Node.ID.Value, new List<Message>());
                });
            });

            nodes.ForEach(node => {
                TaskCompletionSource<bool> waitForNodeViewChange = new TaskCompletionSource<bool>();
                
                node.Communication.OnMessageReceived += (Node sender, Message msg) => {
                    messages[node.Node.ID.Value][sender.ID.Value].Add(msg);
                    
                    var myMessages = messages[node.Node.ID.Value];
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
            FakeNode[] nodes = new FakeNode[15];
            int joinTimeout = 100; //ms
            int startupTime = 50;

            for(int i = 0; i < nodes.Length; i++)
                nodes[i] = new FakeNode(i, i == 0, networkBus, _output, joinTimeout);

            await Task.Run(async () =>
            {
                Task[] joinAwaiters = SetupGroupJoinAwaiters(nodes[0], nodes[1..]);
                Task completed;
                await Task.WhenAll(
                    NodeToolkit.StartSequence(nodes, startupTime),
                    Task.WhenAny(completed = Task.WhenAll(joinAwaiters))//, 
                                   //Task.Delay(Math.Max(nodes.Length * joinTimeout * 10, startupTime*nodes.Length + joinTimeout * 10))) //Worst Case delay
                );

                if(completed.IsCompleted)
                    _output.WriteLine($"|{DateTime.Now.ToString("hh:mm:ss.fff")}|\tCompleted before timeout!");
                else
                    _output.WriteLine($"|{DateTime.Now.ToString("hh:mm:ss.fff")}|\t============ TIMEOUT ===============");
                AssertGroupJoinView(nodes);
            });
        }

        private Task[] SetupGroupJoinAwaiters(FakeNode coordinator, params FakeNode[] nodes)
        {
            List<Task> waitForNodes = new List<Task>();

            nodes.ForEach(node => {
                TaskCompletionSource<bool> waitForNodeViewChange = new TaskCompletionSource<bool>();
                
                node.Group.View.ViewChanged += () => {
                    if(node.Group.View.Others.Count == nodes.Length && node.Group.View.Coordinator == node.Registry.GetOrCreate(coordinator.Node))
                        waitForNodeViewChange.SetResult(true);
                };

                waitForNodes.Add(waitForNodeViewChange.Task);
            });

            return waitForNodes.ToArray();
        }

        private void AssertGroupJoinView(params FakeNode[] nodes)
        {
            Node oneCoordinator = nodes[0].Group.View.Coordinator;
            foreach(FakeNode node in nodes)
            {
                Assert.True(node.Group.View != null);
                Assert.True(node.Group.View.Others.Count == nodes.Length - 1);
                Assert.True(node.Group.View.Coordinator == node.Registry.GetOrCreate(oneCoordinator));

                foreach(FakeNode otherNode in nodes)
                    if(otherNode != node)
                        Assert.Contains(node.Registry.GetOrCreate(otherNode.Node), node.Group.View.Others);
            }
        }


        [Fact]
        public async Task SimpleInViewSend()
        {
            StubNetworkBus networkBus = new StubNetworkBus(new Random().Next());//123); //3 before 2
            FakeNode[] nodes = new FakeNode[15];
            int joinTimeout = 100; //ms
            int maxTestTime = 1000;

            for(int i = 0; i < nodes.Length; i++)
                nodes[i] = new FakeNode(i, i == 0, networkBus, _output, joinTimeout);

            await Task.Run(async () =>
            {
                NodeToolkit.CreateView(nodes, nodes[0]);
                await NodeToolkit.StartSequence(nodes, 0);
                AssertGroupJoinView(nodes);
            });

            Dictionary<int, List<IdMessage>> consolidatedMessages = new Dictionary<int, List<IdMessage>>();
            nodes.ForEach(node => {
                consolidatedMessages.Add(node.Node.ID.Value, new List<IdMessage>());
                node.Group.OnMessageReceived += (sender, message) => {
                    if(message is IdMessage consolidatedMessage)
                    {
                        _output.WriteLine($"{node.Node.ID} consolidated {consolidatedMessage.Id}");
                        consolidatedMessages[node.Node.ID.Value].Add(consolidatedMessage);
                    }
                };
            });

            await Task.Run(async () =>
            {
                await Task.WhenAny(Task.WhenAll(SetupGroupSendAwaiters(nodes[0], nodes[1..])), 
                                   Task.Delay(maxTestTime)); //Worst Case delay
                AssertGroupSend(consolidatedMessages, nodes);
            });
        }

        private Task[] SetupGroupSendAwaiters(FakeNode coordinator, params FakeNode[] nodes)
        {
            List<Task> waitForNodes = new List<Task>();

            int i = 0;
            nodes.ForEach(node => {
                waitForNodes.Add(node.Group.SendMulticast(new IdMessage(i, node.TimeStamper)));
            });

            return waitForNodes.ToArray();
        }

        private void AssertGroupSend(Dictionary<int, List<IdMessage>> _consolidatedMessage, params FakeNode[] nodes)
        {
            AssertGroupJoinView(nodes);
            
            List<IdMessage> referenceMessage = null;
            foreach(var messages in _consolidatedMessage.Values)
            {
                if(referenceMessage == null)
                    referenceMessage = messages;
                else
                    Assert.True(referenceMessage.SequenceEqual(messages)); //In this case since we are on the same machine we expect also the message references to be the same
            }

            Assert.NotNull(referenceMessage);
            Assert.True(referenceMessage.Count > 0);
        }
    }
}
