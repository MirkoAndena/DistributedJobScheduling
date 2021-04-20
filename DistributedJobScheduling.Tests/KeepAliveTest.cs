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
    public class FastKeepAliveManager : KeepAliveManager
    {
        public FastKeepAliveManager(IGroupViewManager group, ILogger logger) : base(group, logger)
        {
            RequestSendTimeout = TimeSpan.FromSeconds(0.2);
            ResponseWindow = TimeSpan.FromSeconds(0.3
            );
        }
    }

    public class KeepAliveNode : FakeNode
    {
        public FastKeepAliveManager KeepAlive { get; private set; }

        public KeepAliveNode(int id, bool coordinator, StubNetworkBus networkBus, ITestOutputHelper logger) : 
        base(id, coordinator, networkBus, logger, 3)
        {
            KeepAlive = new FastKeepAliveManager(base.Group, base.Logger);
            _subSystems.Add(KeepAlive);
        }
    }

    public class KeepAliveTest : IDisposable
    {
        private StubNetworkBus _networkBus;
        private KeepAliveNode[] _nodes;
        private TimeSpan evaluationDelay = TimeSpan.FromSeconds(5);
        private TimeSpan killDelay = TimeSpan.FromSeconds(2);
        private bool _coordinatorDied, _workerDied;
        
        public KeepAliveTest(ITestOutputHelper output)
        {
            _networkBus = new StubNetworkBus(new Random().Next());
            _nodes = CreateGroup(output);
            _coordinatorDied = false;
            _workerDied = false;
        }

        private KeepAliveNode[] CreateGroup(ITestOutputHelper output)
        {
            KeepAliveNode[] nodes = new KeepAliveNode[3];
            nodes[0] = new KeepAliveNode(0, true, _networkBus, output);
            nodes[1] = new KeepAliveNode(1, false, _networkBus, output);
            nodes[2] = new KeepAliveNode(2, false, _networkBus, output);
            return nodes;
        }

        [Fact]
        public async void EveryoneLives()
        {
            StartKeepAlive();

            await Task.Delay(evaluationDelay).ContinueWith(t => 
            {
                Assert.False(_coordinatorDied);
                Assert.False(_workerDied);
            });
        }

        [Fact]
        public async void WorkerDie()
        {
            StartKeepAlive();

            Task killAfterDelay = Task.Delay(killDelay).ContinueWith(t => _nodes[1].Shutdown());

            Task assertResult = Task.Delay(evaluationDelay).ContinueWith(t => 
            {
                Assert.False(_coordinatorDied);
                Assert.True(_workerDied);
            });

            await Task.WhenAll(killAfterDelay, assertResult);
        }

        [Fact]
        public async void CoordinatorDie()
        {
            StartKeepAlive();

            Task killAfterDelay = Task.Delay(killDelay).ContinueWith(t => _nodes[0].Shutdown());

            Task assertResult = Task.Delay(evaluationDelay).ContinueWith(t => 
            {
                Assert.True(_coordinatorDied);
                Assert.False(_workerDied);
            });

            await Task.WhenAll(killAfterDelay, assertResult);
        }

        public void StartKeepAlive()
        {
            _nodes[0].Group.View.ViewChanged += () => _workerDied = _nodes[0].Group.View.Count != 3;
            _nodes[1].Group.View.ViewChanged += () => _coordinatorDied = !_nodes[1].Group.View.CoordinatorExists;
            _nodes[2].Group.View.ViewChanged += () => _coordinatorDied = !_nodes[2].Group.View.CoordinatorExists;

            NodeToolkit.StartSequence(_nodes, 50).Wait();
            NodeToolkit.CreateView(_nodes, _nodes[0]);
        }

        public void Dispose()
        {
            _nodes.ForEach(node => node.Shutdown());
            _networkBus.Dispose();
        }
    }
}