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
    public class KeepAliveManager : IInitializable
    {
        public static TimeSpan RequestSendTimeout = TimeSpan.FromSeconds(10);
        public static TimeSpan ResponseWindow = TimeSpan.FromSeconds(5);

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
        }

        public void Init()
        {
            _logger.Log(Tag.KeepAlive, "Registered to ViewChanged and ViewChanging events");
            _group.View.ViewChanged += OnViewChanged;
            _group.ViewChanging += OnViewChanging;
        }

        private void OnViewChanging()
        {
            if (_keepAlive != null) 
            {
                _logger.Log(Tag.KeepAlive, "Keep-alive stopped");
                _keepAlive.Stop();

                // Unregister from previous events
                if (_keepAlive is CoordinatorKeepAlive coordinatorKeepAlive)
                    coordinatorKeepAlive.NodesDied -= OnNodesDied;    
                if (_keepAlive is WorkersKeepAlive workersKeepAlive)
                    workersKeepAlive.CoordinatorDied -= OnCoordinatorDied;

                _keepAlive = null;
            }
        } 

        private void OnViewChanged()
        {
            OnViewChanging();
            if (_group.View.CoordinatorExists)
            {
                // Group has coordinator so keep-alive can start
                _logger.Log(Tag.KeepAlive, "View changed with coordinator alive, start keep-alive");

                // Create and start proper keep-alive handler
                if (_group.View.ImCoordinator) StartCoordinatorKeepAlive();
                else StartWorkerKeepAlive();
            }
            else
            {
                // Coordinator has crashed so keep-alive suspended
                _logger.Log(Tag.KeepAlive, "View changed with no coordinator, keep-alive can't start yet");
            }
        }

        private void StartCoordinatorKeepAlive()
        {
            _logger.Log(Tag.KeepAlive, "Starting coordinator keep-alive");
            _keepAlive = new CoordinatorKeepAlive(_group, _logger);
            ((CoordinatorKeepAlive)_keepAlive).NodesDied += OnNodesDied;
            _keepAlive.Start();
        }

        private void OnNodesDied(List<Node> nodes) 
        {
            _group.NotifyViewChanged(new HashSet<Node>(nodes), Operation.Left);
        }

        private void StartWorkerKeepAlive()
        {
            _logger.Log(Tag.KeepAlive, "Starting workers keep-alive");
            _keepAlive = new WorkersKeepAlive(_group, _logger);
            ((WorkersKeepAlive)_keepAlive).CoordinatorDied += OnCoordinatorDied;
            _keepAlive.Start();
        }

        private void OnCoordinatorDied()
        {
            _group.NotifyViewChanged(new HashSet<Node>(new [] { _group.View.Coordinator} ), Operation.Left);
        }
    }
}