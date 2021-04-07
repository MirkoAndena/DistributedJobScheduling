using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Communication.Messaging;

namespace DistributedJobScheduling.LeaderElection.KeepAlive
{
    public class KeepAliveManager : IStartable
    {
        public Action CoordinatorDied;
        public Action<List<Node>> NodesDied;

        private IStartable _keepAlive;
        private ILogger _logger;
        private IGroupViewManager _group;

        public KeepAliveManager() : this (
            DependencyInjection.DependencyManager.Get<IGroupViewManager>(),
            DependencyInjection.DependencyManager.Get<ILogger>()) {}

        public KeepAliveManager(IGroupViewManager group, ILogger logger)
        {
            _logger = logger;
            _group = group;
            group.View.ViewChanged += () => OnViewChanged(group.View.Coordinator);
            group.ViewChanging += Stop;
        }

        private void OnViewChanged(Node coordinator)
        {
            Stop();

            if (coordinator != null)
            {
                // Group has coordinator so keep-alive can start
                _logger.Log(Tag.KeepAlive, "View changed with coordinator alive");

                // Unregister from previous events
                if (_keepAlive is CoordinatorKeepAlive coordinatorKeepAlive)
                    coordinatorKeepAlive.NodesDied -= OnNodesDied;    
                if (_keepAlive is WorkersKeepAlive workersKeepAlive)
                    workersKeepAlive.CoordinatorDied -= OnCoordinatorDied;

                // Create and start proper keep-alive handler
                if (_group.View.ImCoordinator) StartCoordinatorKeepAlive();
                else StartWorkerKeepAlive();
            }
            else
            {
                // Coordinator has crashed so keep-alive suspended
                _logger.Log(Tag.KeepAlive, "View changed with coordinator died");
                _keepAlive.Stop();
            }
        }

        private void StartCoordinatorKeepAlive()
        {
            _keepAlive = new CoordinatorKeepAlive(_group, _logger);
            ((CoordinatorKeepAlive)_keepAlive).NodesDied += OnNodesDied;
            _keepAlive.Start();
        }

        private void OnNodesDied(List<Node> nodes) 
        {
            NodesDied?.Invoke(nodes);
            _group.NotifyViewChanged(new HashSet<Node>(nodes), ViewChangeOperation.Left);
        }

        private void StartWorkerKeepAlive()
        {
            _keepAlive = new WorkersKeepAlive(_group, _logger);
            ((WorkersKeepAlive)_keepAlive).CoordinatorDied += OnCoordinatorDied;
            _keepAlive.Start();
        }

        private void OnCoordinatorDied()
        {
            _group.NotifyViewChanged(new HashSet<Node>(new [] { _group.View.Coordinator} ), ViewChangeOperation.Left);
            CoordinatorDied?.Invoke();
        }

        public void Start()
        {
            // First start is not needed
        }

        public void Stop()
        {
            if (_keepAlive != null) 
                _keepAlive.Stop();
        } 
    }
}