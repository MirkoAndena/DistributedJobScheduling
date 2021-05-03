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
    public class WorkersKeepAlive : IStartable
    {
        public Action CoordinatorDied;
        private ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private IGroupViewManager _groupManager;

        public WorkersKeepAlive(IGroupViewManager group, ILogger logger)
        {
            _groupManager = group;
            _logger = logger;
        }
        
        public void Start()
        {
            var jobPublisher = _groupManager.Topics.GetPublisher<KeepAlivePublisher>();
            jobPublisher.RegisterForMessage(typeof(KeepAliveRequest), OnKeepAliveRequestReceived);

            _cancellationTokenSource = new CancellationTokenSource();
            Task.Delay(KeepAliveManager.WorkerRequestWindow, _cancellationTokenSource.Token)
                .ContinueWith(t =>  { if (!t.IsCanceled) TimeoutFinished(); });
        }

        public void Stop()
        {
            var jobPublisher = _groupManager.Topics.GetPublisher<KeepAlivePublisher>();
            jobPublisher.UnregisterForMessage(typeof(KeepAliveRequest), OnKeepAliveRequestReceived);

            _cancellationTokenSource?.Cancel();
        }

        private void OnKeepAliveRequestReceived(Node node, Message message)
        {
            _groupManager.Send(node, new KeepAliveResponse((KeepAliveRequest)message)).Wait();
            _logger.Log(Tag.KeepAlive, "Sent keep-alive response to coordinator, i'm alive");
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Delay(KeepAliveManager.WorkerRequestWindow, _cancellationTokenSource.Token)
                .ContinueWith(t =>  { if (!t.IsCanceled) TimeoutFinished(); });
        }

        private void TimeoutFinished()
        {
            _logger.Warning(Tag.KeepAlive, "No keep alive request arrived, coordinator has crashed");
            CoordinatorDied?.Invoke();
            this.Stop();
        }
    }
}