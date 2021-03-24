using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.LeaderElection.KeepAlive
{
    public class WorkersKeepAlive : IStartable, IInitializable
    {
        private int ReceiveTimeout = CoordinatorKeepAlive.SendTimeout * 2;
        public Action CoordinatorDied;
        private ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private IGroupViewManager _groupManager;

        public WorkersKeepAlive(IGroupViewManager group, ILogger logger)
        {
            _groupManager = group;
            _logger = logger;
        }

        public void Init()
        {
            var jobPublisher = _groupManager.Topics.GetPublisher<KeepAlivePublisher>();
            jobPublisher.RegisterForMessage(typeof(KeepAliveRequest), OnKeepAliveRequestReceived);
        }
        
        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Delay(TimeSpan.FromSeconds(ReceiveTimeout), _cancellationTokenSource.Token)
                .ContinueWith(t => TimeoutFinished());
        }

        public void Stop() => _cancellationTokenSource?.Cancel();

        private void OnKeepAliveRequestReceived(Node node, Message message)
        {
            _logger.Log(Tag.KeepAlive, "Received keep-alive request from coordinator");
            _groupManager.Send(node, new KeepAliveResponse((KeepAliveRequest)message)).Wait();
            _logger.Log(Tag.KeepAlive, "I'm alive");
            Stop();
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