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
    public class CoordinatorKeepAlive : ILifeCycle
    {
        // Seconds delay of coordintator to send keepAlive message
        public static int SendTimeout = 5;
        private int ReceiveTimeout = SendTimeout * 2;

        public Action<List<Node>> NodesDied;
        private Dictionary<Node, bool> _ticks;
        private ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;

        private IGroupViewManager _group;

        public CoordinatorKeepAlive(IGroupViewManager group, ILogger logger)
        {
            _group = group;
            _logger = logger;
            _ticks = new Dictionary<Node, bool>();

            var jobPublisher = _group.Topics.GetPublisher<BullyElectionPublisher>();
            jobPublisher.RegisterForMessage(typeof(KeepAliveResponse), OnKeepAliveResponseReceived);
        }

        public void Init()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _ticks.Clear();
            _group.View.Others.ForEach(node => _ticks.Add(node, false));
        }
        
        public void Start()
        {
            Task.Delay(TimeSpan.FromSeconds(SendTimeout), _cancellationTokenSource.Token)
                .ContinueWith(t => SendKeepAliveToNodes());
            Task.Delay(TimeSpan.FromSeconds(ReceiveTimeout), _cancellationTokenSource.Token)
                .ContinueWith(t => TimeoutFinished());
        }

        public void Stop() => _cancellationTokenSource.Cancel();

        private void SendKeepAliveToNodes()
        {
            _group.View.Others.ForEach(node => _group.Send(node, new KeepAliveRequest()).Wait());
            Task.Delay(TimeSpan.FromSeconds(SendTimeout)).ContinueWith(t => SendKeepAliveToNodes());
        }

        private void OnKeepAliveResponseReceived(Node node, Message message) => _ticks.Add(node, true);

        private void TimeoutFinished()
        {
            List<Node> deaths = new List<Node>();
            _ticks.ForEach(element => 
            {
                if (!element.Value)
                    deaths.Add(element.Key);
            });

            if (deaths.Count > 0)
            {
                _logger.Log(Tag.KeepAlive, $"Nodes {deaths} died");
                NodesDied?.Invoke(deaths);
            }

            Init();
            Start();
        }
    }
}