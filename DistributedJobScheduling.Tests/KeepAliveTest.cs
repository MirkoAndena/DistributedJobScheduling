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
using DistributedJobScheduling.LifeCycle;

namespace DistributedJobScheduling.DistributedStorage
{
    public class CoordinatorNode : FakeNode
    {
        public CoordinatorKeepAlive KeepAlive { get; private set; }

        public CoordinatorNode(int id, bool coordinator, StubNetworkBus networkBus, ITestOutputHelper logger) : 
        base(id, coordinator, networkBus, logger, 3)
        {
            KeepAlive = new CoordinatorKeepAlive(base.Group, base.Logger);
            _subSystems.Add(KeepAlive);
        }
    }

    public class WorkerNode : FakeNode
    {
        public WorkersKeepAlive KeepAlive { get; private set; }

        public WorkerNode(int id, bool coordinator, StubNetworkBus networkBus, ITestOutputHelper logger) : 
        base(id, coordinator, networkBus, logger, 3)
        {
            KeepAlive = new WorkersKeepAlive(base.Group, base.Logger);
            _subSystems.Add(KeepAlive);
        }
    }

    public class KeepAliveTest : IDisposable
    {
        private ILogger _logger;
        private ITimeStamper _timeStamper;
        private ITestOutputHelper _output;
        private StubNetworkBus _networkBus;
        private FakeNode[] _nodes;
        private CoordinatorNode _coordinatorNode => (CoordinatorNode)_nodes[0];
        private WorkerNode _workerNode1 => (WorkerNode)_nodes[1];
        private WorkerNode _workerNode2 => (WorkerNode)_nodes[2];
        
        public KeepAliveTest(ITestOutputHelper output)
        {
            _output = output;
            _networkBus = new StubNetworkBus(new Random().Next());
            _logger = new StubLogger(_output);
            _nodes = CreateGroup(_output);
        }

        private FakeNode[] CreateGroup(ITestOutputHelper output)
        {
            FakeNode[] nodes = new FakeNode[3];
            nodes[0] = new CoordinatorNode(0, true, _networkBus, output);
            nodes[1] = new WorkerNode(1, false, _networkBus, output);
            nodes[2] = new WorkerNode(2, false, _networkBus, output);

            NodeToolkit.CreateView(nodes, nodes[0]);
            return nodes;
        }

        [Fact]
        public async void EveryoneLives()
        {
            bool coordDeath = false;
            bool someoneDeath = false;
            StartKeepAlive(() => coordDeath = true, nodes => someoneDeath = nodes.Count > 0);

            await Task.Delay(TimeSpan.FromSeconds(CoordinatorKeepAlive.ReceiveTimeout * 3)).ContinueWith(t => 
            {
                Assert.False(coordDeath);
                Assert.False(someoneDeath);
            });
        }

        [Fact]
        public async void WorkerDie()
        {
            bool coordDeath = false;
            bool someoneDeath = false;
            StartKeepAlive(() => coordDeath = true, nodes => someoneDeath = nodes.Count > 0);

            Task.Delay(TimeSpan.FromSeconds(CoordinatorKeepAlive.ReceiveTimeout)).ContinueWith(t => _workerNode1.Shutdown());

            await Task.Delay(TimeSpan.FromSeconds(CoordinatorKeepAlive.ReceiveTimeout * 3)).ContinueWith(t => 
            {
                Assert.False(coordDeath);
                Assert.True(someoneDeath);
            });
        }

        [Fact]
        public async void CoordinatorDie()
        {
            bool coordDeath = false;
            bool someoneDeath = false;
            StartKeepAlive(() => coordDeath = true, nodes => someoneDeath = nodes.Count > 0);

            Task.Delay(TimeSpan.FromSeconds(CoordinatorKeepAlive.ReceiveTimeout)).ContinueWith(t => _coordinatorNode.Shutdown());

            await Task.Delay(TimeSpan.FromSeconds(CoordinatorKeepAlive.ReceiveTimeout * 3)).ContinueWith(t => 
            {
                Assert.True(coordDeath);
                Assert.False(someoneDeath);
            });
        }

        public void StartKeepAlive(Action coordinatorDeath, Action<List<Node>> workerDeath)
        {
            _coordinatorNode.KeepAlive.NodesDied += workerDeath;
            _workerNode1.KeepAlive.CoordinatorDied += coordinatorDeath;
            _workerNode2.KeepAlive.CoordinatorDied += coordinatorDeath;
            NodeToolkit.StartSequence(_nodes, 50).Wait();
        }

        public void Dispose()
        {
            _nodes.ForEach(node => node.Shutdown());
            _networkBus.Dispose();
        }
    }
}