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
    public class CoordinatorKeepAlive : IStartable
    {
        // Seconds delay of coordintator to send keepAlive message
        public static int SendTimeout = 5;
        private int ReceiveTimeout = SendTimeout * 2;

        public Action<List<Node>> NodesDied;
        private Dictionary<Node, bool> _ticks;
        private ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;

        private IGroupViewManager _groupManager;

        public CoordinatorKeepAlive(IGroupViewManager group, ILogger logger)
        {
            _groupManager = group;
            _logger = logger;
            _ticks = new Dictionary<Node, bool>();
        }
        
        public void Start()
        {
            _logger.Log(Tag.KeepAlive, "Starting coordinator keep alive service...");
            
            var jobPublisher = _groupManager.Topics.GetPublisher<KeepAlivePublisher>();
            jobPublisher.RegisterForMessage(typeof(KeepAliveResponse), OnKeepAliveResponseReceived);

            _cancellationTokenSource = new CancellationTokenSource();
            ResetTicks();
            Task.Delay(TimeSpan.FromSeconds(SendTimeout), _cancellationTokenSource.Token)
                .ContinueWith(t => { if (!t.IsCanceled) SendKeepAliveToNodes(); });
            Task.Delay(TimeSpan.FromSeconds(ReceiveTimeout), _cancellationTokenSource.Token)
                .ContinueWith(t => { if (!t.IsCanceled) TimeoutFinished(); });
        }

        public void Stop() 
        {
            var jobPublisher = _groupManager.Topics.GetPublisher<KeepAlivePublisher>();
            jobPublisher.UnregisterForMessage(typeof(KeepAliveResponse), OnKeepAliveResponseReceived);

            _cancellationTokenSource?.Cancel();
        }

        private void ResetTicks()
        {
            _ticks.Clear();
            _groupManager.View.Others.ForEach(node => _ticks.Add(node, false));
        }

        private void SendKeepAliveToNodes()
        {
            _groupManager.View.Others.ForEach(node => 
            {
                _groupManager.Send(node, new KeepAliveRequest()).Wait();
                _logger.Log(Tag.KeepAlive, $"Sent keep-alive request to {node}");
            });
            Task.Delay(TimeSpan.FromSeconds(SendTimeout), _cancellationTokenSource.Token)
                .ContinueWith(t => { if (!t.IsCanceled) SendKeepAliveToNodes(); });
        }

        private void OnKeepAliveResponseReceived(Node node, Message message)
        { 
            if (_ticks.ContainsKey(node))
            {
                _ticks[node] = true;
                _logger.Log(Tag.KeepAlive, $"Received keep-alive response from {node}");
            }
            else
            {
                _logger.Warning(Tag.KeepAlive, $"Received keep-alive response from {node} that's not in view");
            }
        }

        private void TimeoutFinished()
        {
            List<Node> deaths = new List<Node>();
            _ticks.ForEach(element => 
            {
                if (!element.Value)
                    deaths.Add(element.Key);
            });
            ResetTicks();

            if (deaths.Count > 0)
            {
                _logger.Warning(Tag.KeepAlive, $"Nodes {deaths.ToString<Node>()} died");
                NodesDied?.Invoke(deaths);
            }

            Task.Delay(TimeSpan.FromSeconds(ReceiveTimeout), _cancellationTokenSource.Token)
                .ContinueWith(t => { if (!t.IsCanceled) TimeoutFinished(); });
        }
    }
}