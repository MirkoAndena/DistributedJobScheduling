using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.LeaderElection.KeepAlive
{
    public class WorkersKeepAlive : ILifeCycle
    {
        private int ReceiveTimeout = CoordinatorKeepAlive.SendTimeout * 2;
        public Action CoordinatorDied;
        private ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private IGroupViewManager _group;

        public WorkersKeepAlive(IGroupViewManager group, ILogger logger)
        {
            _group = group;
            _logger = logger;

            var jobPublisher = _group.Topics.GetPublisher<BullyElectionPublisher>();
            jobPublisher.RegisterForMessage(typeof(KeepAliveRequest), OnKeepAliveRequestReceived);
        }

        public void Init()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        public void Start()
        {
            Task.Delay(TimeSpan.FromSeconds(ReceiveTimeout), _cancellationTokenSource.Token)
                .ContinueWith(t => TimeoutFinished());
        }

        public void Stop() => _cancellationTokenSource.Cancel();

        private void OnKeepAliveRequestReceived(Node node, Message message)
        {
            KeepAliveRequest received = (KeepAliveRequest)message;
            _group.Send(node, new KeepAliveResponse(received)).Wait();
            _logger.Log(Tag.KeepAlive, "I'm alive");
            Stop();
            Init();
            Start();
        }

        private void TimeoutFinished()
        {
            _logger.Log(Tag.KeepAlive, "No keep alive request arrived, coordinator has crashed");
            CoordinatorDied?.Invoke();
            Stop();
        }
    }
}