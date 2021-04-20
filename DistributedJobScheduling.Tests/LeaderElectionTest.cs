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
using DistributedJobScheduling.LeaderElection;

namespace DistributedJobScheduling.DistributedStorage
{
    public class FastBullyElectionMessageHandler : BullyElectionMessageHandler
    {
        public FastBullyElectionMessageHandler(ILogger logger, IGroupViewManager groupViewManager, Node.INodeRegistry nodeRegistry) : 
        base(logger, groupViewManager, nodeRegistry)
        {
            ResponseWindow = TimeSpan.FromSeconds(0.2);
        }
    }

    public class LeaderElectionNode : FakeNode
    {
        public FastBullyElectionMessageHandler LeaderElection { get; private set; }

        public LeaderElectionNode(int id, bool coordinator, StubNetworkBus networkBus, ITestOutputHelper logger) : 
        base(id, coordinator, networkBus, logger, 3)
        {
            LeaderElection = new FastBullyElectionMessageHandler(base.Logger, base.Group, base.Registry);
            _subSystems.Add(LeaderElection);
        }
    }

    public class LeaderElectionTest : IDisposable
    {
        private ILogger _logger;
        private ITimeStamper _timeStamper;
        private ITestOutputHelper _output;
        private StubNetworkBus _networkBus;
        private LeaderElectionNode[] _nodes;
        private TimeSpan evaluationDelay = TimeSpan.FromSeconds(5);
        private bool _coordinatorElected;
        
        public LeaderElectionTest(ITestOutputHelper output)
        {
            _output = output;
            _networkBus = new StubNetworkBus(new Random().Next());
            _logger = new StubLogger(_output);
            _nodes = CreateGroup(_output);
            _coordinatorElected = false;
        }

        private LeaderElectionNode[] CreateGroup(ITestOutputHelper output)
        {
            LeaderElectionNode[] nodes = new LeaderElectionNode[3];
            nodes[0] = new LeaderElectionNode(0, false, _networkBus, output);
            nodes[1] = new LeaderElectionNode(1, false, _networkBus, output);
            nodes[2] = new LeaderElectionNode(2, false, _networkBus, output);
            return nodes;
        }

        [Fact]
        public async void ElectionComplete()
        {    
            NodeToolkit.StartSequence(_nodes, 50).Wait();
            NodeToolkit.CreateView(_nodes, _nodes[0]);

            await Task.Delay(evaluationDelay).ContinueWith(t => 
            {
                Assert.Equal(2, _nodes[0].Group.View.Coordinator.ID.Value);
                Assert.Equal(2, _nodes[1].Group.View.Coordinator.ID.Value);
                Assert.Equal(2, _nodes[2].Group.View.Coordinator.ID.Value);
                Assert.True(_nodes[0].Group.View.ImCoordinator);
            });
        }

        public void Dispose()
        {
            _nodes.ForEach(node => node.Shutdown());
            _networkBus.Dispose();
        }
    }
}