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

        public KeepAliveManager() : this (
            DependencyInjection.DependencyManager.Get<IGroupViewManager>(),
            DependencyInjection.DependencyManager.Get<ILogger>()) {}

        public KeepAliveManager(IGroupViewManager group, ILogger logger)
        {
            if (group.View.ImCoordinator)
            {
                _keepAlive = new CoordinatorKeepAlive(group, logger);
                ((CoordinatorKeepAlive)_keepAlive).NodesDied += nodes => 
                {
                    NodesDied?.Invoke(nodes);
                    group.NotifyViewChanged(new HashSet<Node>(nodes), ViewChangeMessage.ViewChangeOperation.Left);
                };
            }
            else
            {
                _keepAlive = new WorkersKeepAlive(group, logger);
                ((WorkersKeepAlive)_keepAlive).CoordinatorDied += () => {
                    group.NotifyViewChanged(new HashSet<Node>(new [] { group.View.Coordinator} ), ViewChangeMessage.ViewChangeOperation.Left);
                    CoordinatorDied?.Invoke();
                };
            }

            group.View.ViewChanged += () => OnViewChanged(group.View.Coordinator);
        }

        private void OnViewChanged(Node coordinator)
        {
            if (coordinator != null)
            {
                // Group has coordinator so keep-alive can start
                ((IInitializable)_keepAlive).Init();
                _keepAlive.Start();
            }
            else
            {
                // Coordinator has crashed so keep-alive suspended
                _keepAlive.Stop();
            }
        }

        private void Restart()
        {
            _keepAlive.Stop();
            ((IInitializable)_keepAlive).Init();
            _keepAlive.Start();
        }

        public void Start()
        {
            // First start is not needed
        }

        public void Stop() => _keepAlive.Stop();
    }
}