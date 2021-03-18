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
namespace DistributedJobScheduling.LeaderElection.KeepAlive
{
    public class KeepAliveMessageHandler : ILifeCycle
    {
        // Seconds for keep alive check
        const int timeout = 5;
        public Action<List<Node>> NodesDied;
        public Action CoordinatorDied;
        private Dictionary<Node, bool> _ticks;
        private ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;

        private GroupViewManager _group;

        public KeepAliveMessageHandler(GroupViewManager group, ILogger logger)
        {
            _group = group;
            _logger = logger;
            _ticks = new Dictionary<Node, bool>();

            var jobPublisher = _group.Topics.GetPublisher<BullyElectionPublisher>();
            jobPublisher.RegisterForMessage(typeof(KeepAliveMessage), OnKeepAliveReceived);
        }

        public void Start()
        {
            Task.Delay(TimeSpan.FromSeconds(timeout), _cancellationTokenSource.Token)
                .ContinueWith(t => TimeoutFinished());
        }

        public void Init()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _ticks.Clear();
            if (_group.View.Coordinator != _group.View.Me)
                _ticks.Add(_group.View.Coordinator, false);
            foreach (Node node in _group.View.Others)
                _ticks.Add(node, false);
        }
        
        private void OnKeepAliveReceived(Node node, Message message) => _ticks.Add(node, true);

        private void TimeoutFinished()
        {
            List<Node> deaths = new List<Node>();
            _ticks.ForEach(element => 
            {
                if (!element.Value)
                {
                    if (element.Key == _group.View.Coordinator)
                    {
                        _logger.Log(Tag.KeepAlive, $"Coordinator missed a keepalive message, it's died");
                        CoordinatorDied?.Invoke();
                    }
                    deaths.Add(element.Key);
                }
            });

            if (deaths.Count > 0)
            {
                _logger.Log(Tag.KeepAlive, $"Nodes {deaths} died");
                NodesDied?.Invoke(deaths);
            }

            Init();
            Start();
        }

        public void Stop() => _cancellationTokenSource.Cancel();
    }
}