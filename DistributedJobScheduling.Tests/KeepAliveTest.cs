using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.LeaderElection.KeepAlive;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Tests.Utils;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit;
using Xunit.Abstractions;
using DistributedJobScheduling.Tests.Communication;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Tests;

namespace DistributedJobScheduling.DistributedStorage
{
    public class KeepAliveTest
    {
        IGroupViewManager _group;
        ILogger _logger;
        ITimeStamper _timeStamper;
        FakeNode _diedNode, _coordinator;
        ITestOutputHelper _output;
        StubNetworkBus _networkBus;
        
        public KeepAliveTest(ITestOutputHelper output)
        {
            _output = output;
            _networkBus = new StubNetworkBus(new Random().Next());
            FakeNode[] nodes = new FakeNode[3];
            for(int i = 0; i < nodes.Length; i++)
                nodes[i] = new FakeNode(i, i == 0, _networkBus, output, 3);
            NodeToolkit.CreateView(nodes, nodes[0]);
            _group = nodes[0].Group;
            _coordinator = nodes[0];
            _diedNode = nodes[1];
            _logger = new StubLogger(_group.View.Me, _output);
            NodeToolkit.StartSequence(nodes, 50).Wait();
        }

        private void KillNode(FakeNode node)
        {
            node.Group.View.Remove(node.Node);
        }

        [Fact]
        public async void WorkerFail()
        {
            bool someoneDies = false;
            bool coordinatorDie = false;

            CoordinatorKeepAlive coordinatorKeepAlive = new CoordinatorKeepAlive(_group, _logger);
            coordinatorKeepAlive.NodesDied += nodes =>
            {
                Assert.Equal(1, nodes.Count);
                Assert.Equal(_diedNode.Node, nodes[0]);
                someoneDies = true;
                _output.WriteLine($"Expected to die: {_diedNode.Node.ToString()}, Nodes died: {nodes.ToString<Node>()}");
            };
            WorkersKeepAlive workersKeepAlive = new WorkersKeepAlive(_group, _logger);
            workersKeepAlive.CoordinatorDied += () =>
            {
                coordinatorDie = true;
            };
            
            workersKeepAlive.Start();
            coordinatorKeepAlive.Start();

            Task.Delay(TimeSpan.FromSeconds(20)).ContinueWith(t => KillNode(_diedNode));

            await Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(t => 
            {
                workersKeepAlive.Stop();
                coordinatorKeepAlive.Stop();
                Assert.True(someoneDies);
                _output.WriteLine($"Some nodes died? {(someoneDies ? "YES" : "NO")}");
                Assert.True(coordinatorDie);
                _output.WriteLine($"Coordinator died? {(coordinatorDie ? "YES" : "NO")}");
                _networkBus.Dispose();
            });
        }
    }
}