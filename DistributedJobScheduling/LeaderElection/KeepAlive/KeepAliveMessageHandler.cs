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
    public class KeepAliveManager : ILifeCycle
    {
        public Action CoordinatorDied;
        public Action<List<Node>> NodesDied;

        private ILifeCycle _keepAlive;

        public KeepAliveManager(IGroupViewManager group, ILogger logger, ITimeStamper timeStamper)
        {
            if (group.View.ImCoordinator)
            {
                _keepAlive = new CoordinatorKeepAlive(group, logger, timeStamper);
                ((CoordinatorKeepAlive)_keepAlive).NodesDied += nodes => NodesDied?.Invoke(nodes);
            }
            else
            {
                _keepAlive = new WorkersKeepAlive(group, logger, timeStamper);
                ((WorkersKeepAlive)_keepAlive).CoordinatorDied += () => CoordinatorDied?.Invoke();
            }

            group.View.ViewChanged += Restart;
        }

        private void Restart()
        {
            Stop();
            Init();
            Start();
        }

        public void Init() => _keepAlive.Init();

        public void Start() => _keepAlive.Start();

        public void Stop() => _keepAlive.Stop();
    }
}