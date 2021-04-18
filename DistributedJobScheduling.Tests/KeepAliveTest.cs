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
    public class KeepAliveTest : IDisposable
    {
        private IGroupViewManager _group;
        private ILogger _logger;
        private ITimeStamper _timeStamper;
        private ITestOutputHelper _output;
        private StubNetworkBus _networkBus;
        private Node _coordinator, _worker;
        private CoordinatorKeepAlive _coordinatorKeepAlive;
        private WorkersKeepAlive _workersKeepAlive;
        
        public KeepAliveTest(ITestOutputHelper output)
        {
            _output = output;
            _networkBus = new StubNetworkBus(new Random().Next());
            _logger = new StubLogger(_output);
            _group = CreateGroup(_output);
        }

        private IGroupViewManager CreateGroup(ITestOutputHelper output)
        {
            FakeNode[] nodes = new FakeNode[3];
            nodes[0] = new FakeNode(0, true, _networkBus, output, 3);
            nodes[1] = new FakeNode(1, false, _networkBus, output, 3);
            nodes[2] = new FakeNode(2, false, _networkBus, output, 3);

            _coordinator = nodes[0].Node;
            _worker = nodes[1].Node;

            NodeToolkit.CreateView(nodes, nodes[0]);
            NodeToolkit.StartSequence(nodes, 50).Wait();
            return nodes[0].Group;
        }

        [Fact]
        public async void EveryoneLives()
        {
            bool coordDeath = false;
            bool someoneDeath = false;
            StartKeepAlive(() => coordDeath = true, nodes => someoneDeath = nodes.Count > 0);

            await Task.Delay(TimeSpan.FromSeconds(20)).ContinueWith(t => 
            {
                Assert.False(coordDeath);
                Assert.False(someoneDeath);
            });
        }

        public void StartKeepAlive(Action coordinatorDeath, Action<List<Node>> workerDeath)
        {
            _coordinatorKeepAlive = new CoordinatorKeepAlive(_group, _logger);
            _coordinatorKeepAlive.NodesDied += workerDeath;

            _workersKeepAlive = new WorkersKeepAlive(_group, _logger);
            _workersKeepAlive.CoordinatorDied += coordinatorDeath;
            
            _workersKeepAlive.Start();
            _coordinatorKeepAlive.Start();
        }

        public void Dispose()
        {
            _coordinatorKeepAlive.Stop();
            _workersKeepAlive.Stop();
            _networkBus.Dispose();
        }
    }
}