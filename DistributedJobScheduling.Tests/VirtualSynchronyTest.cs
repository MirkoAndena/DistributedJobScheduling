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
            using(StubNetworkBus networkBus = new StubNetworkBus(new Random().Next()))
            {
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
                        Message message = new EmptyMessage();
                        await node.Communication.SendMulticast(message.ApplyStamp(node.TimeStamper));
                    }
                });
                
                await Task.Run(async () =>
                {
                    await receiveTask;
                    AssertMulticasts(messages, messageCount);
                });
            }
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
                TaskCompletionSource<bool> waitForNodeViewChange = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                
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
            using(StubNetworkBus networkBus = new StubNetworkBus(new Random().Next()))
            {
                FakeNode[] nodes = new FakeNode[10];
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
                        Task.WhenAny(completed = Task.WhenAll(joinAwaiters), 
                                    Task.Delay(Math.Max(nodes.Length * joinTimeout * 10, startupTime*nodes.Length + joinTimeout * 10))) //Worst Case delay
                    );

                    if(completed.IsCompleted)
                        _output.WriteLine($"|{DateTime.Now.ToString("hh:mm:ss.fff")}|\tCompleted before timeout!");
                    else
                        _output.WriteLine($"|{DateTime.Now.ToString("hh:mm:ss.fff")}|\t============ TIMEOUT ===============");
                    AssertGroupJoinView(nodes);
                });
            }
        }


        private Task[] SetupGroupJoinAwaiters(FakeNode coordinator, params FakeNode[] nodes)
        {
            List<Task> waitForNodes = new List<Task>();

            nodes.ForEach(node => {
                TaskCompletionSource<bool> waitForNodeViewChange = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                
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
            using(StubNetworkBus networkBus = new StubNetworkBus(new Random().Next()))
            {
                FakeNode[] nodes = new FakeNode[10];
                int joinTimeout = 0; //ms
                int maxTestTime = 1000 * nodes.Length;

                for(int i = 0; i < nodes.Length; i++)
                    nodes[i] = new FakeNode(i, i == 0, networkBus, _output, joinTimeout);

                await Task.Run(async () =>
                {
                    NodeToolkit.CreateView(nodes, nodes[0]);
                    await NodeToolkit.StartSequence(nodes, 0);
                    AssertGroupJoinView(nodes);
                });
                
                var consolidatedMessages = new Dictionary<int, HashSet<IdMessage>>();
                List<IdMessage> sentMessages = new List<IdMessage>(nodes.Length);
                Task[] waitForAllMulticasts = SetupGroupSendAwaiters(consolidatedMessages, nodes.Length - 1, nodes);
                //Send Messages
                nodes.ForEach(node => {
                    var message = new IdMessage(node.Node.ID.Value);
                    message.SenderID = node.Node.ID.Value;
                    sentMessages.Add(message);
                    node.Group.SendMulticast(message);
                });

                await Task.Run(async () =>
                {
                    await Task.WhenAll(Task.WhenAny(Task.WhenAll(waitForAllMulticasts), 
                                    Task.Delay(maxTestTime))); //Worst Case delay
                    AssertGroupSend(consolidatedMessages, sentMessages, nodes);
                });
            }
        }

        private Task[] SetupGroupSendAwaiters(Dictionary<int, HashSet<IdMessage>> consolidatedMessages, int expectedMessages, params FakeNode[] nodes)
        {
            List<Task> waitForNodes = new List<Task>();

            nodes.ForEach(node => {
                consolidatedMessages.Add(node.Node.ID.Value, new HashSet<IdMessage>());
                TaskCompletionSource<bool> waitForMessages = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                node.Group.OnMessageReceived += (sender, message) => {
                    if(message is IdMessage consolidatedMessage)
                    {
                        _output.WriteLine($"{node.Node.ID} consolidated {consolidatedMessage.Id}");
                        consolidatedMessages[node.Node.ID.Value].Add(consolidatedMessage);
                        if(!waitForMessages.Task.IsCompleted &&
                            consolidatedMessages[node.Node.ID.Value].Count >= expectedMessages)
                            waitForMessages.SetResult(true);
                    }
                };
                waitForNodes.Add(waitForMessages.Task);
            });

            return waitForNodes.ToArray();
        }

        private void AssertGroupSend(Dictionary<int, HashSet<IdMessage>> _consolidatedMessage, List<IdMessage> sentMessages, params FakeNode[] nodes)
        {
            HashSet<IdMessage> referenceMessage = null;
            foreach(var node in _consolidatedMessage.Keys)
            {
                var messages = _consolidatedMessage[node];
                referenceMessage = new HashSet<IdMessage>(sentMessages.Where(n => n.SenderID != node));
                Assert.True(referenceMessage.SetEquals(messages)); //In this case since we are on the same machine we expect also the message references to be the same
            }

            Assert.NotNull(referenceMessage);
            Assert.True(referenceMessage.Count > 0);
        }
    }
}
