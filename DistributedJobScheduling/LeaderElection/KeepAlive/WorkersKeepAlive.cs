using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive;
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

        private List<KeepAliveRequest> _requestQueue;

        public WorkersKeepAlive(IGroupViewManager group, ILogger logger, List<KeepAliveRequest> requestQueue)
        {
            _groupManager = group;
            _logger = logger;
            _requestQueue = new List<KeepAliveRequest>(requestQueue);
        }
        
        public void Start()
        {
            var jobPublisher = _groupManager.Topics.GetPublisher<KeepAlivePublisher>();
            jobPublisher.RegisterForMessage(typeof(KeepAliveRequest), OnKeepAliveRequestReceived);

            _logger.Log(Tag.KeepAlive, $"Replaying {_requestQueue.Count} keep alive requests");
            foreach(var request in _requestQueue.ToArray())
                SendResponseToCoordinator(request);
            _requestQueue = null;

            //If the coordinator never sent a keep-alive, timeout after a window
            CancelWindowTimeout();
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
            SendResponseToCoordinator((KeepAliveRequest)message);
            
            CancelWindowTimeout();
            Task.Delay(KeepAliveManager.WorkerRequestWindow, _cancellationTokenSource.Token)
                .ContinueWith(t =>  { if (!t.IsCanceled) TimeoutFinished(); });
        }

        private void SendResponseToCoordinator(KeepAliveRequest request)
        {
            try
            {
                _groupManager.Send(_groupManager.View.Coordinator, new KeepAliveResponse(request)).Wait();
                _logger.Log(Tag.KeepAlive, "Sent keep-alive response to coordinator, i'm alive");
            }
            catch (NotDeliveredException e)
            {
               _logger.Warning(Tag.KeepAlive, "keep-alive response not delivered", e);
            }
        }

        private void CancelWindowTimeout()
        {
            lock(this)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        private void TimeoutFinished()
        {
            _logger.Warning(Tag.KeepAlive, "No keep alive request arrived, coordinator has crashed");
            CoordinatorDied?.Invoke();
            this.Stop();
        }
    }
}