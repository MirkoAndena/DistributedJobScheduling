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
        public Action<List<Node>> NodesDied;
        private List<Node> _ticks;
        private ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;

        private IGroupViewManager _groupManager;

        public CoordinatorKeepAlive(IGroupViewManager group, ILogger logger)
        {
            _groupManager = group;
            _logger = logger;
            _ticks = new List<Node>();
        }
        
        public void Start()
        {            
            var jobPublisher = _groupManager.Topics.GetPublisher<KeepAlivePublisher>();
            jobPublisher.RegisterForMessage(typeof(KeepAliveResponse), OnKeepAliveResponseReceived);

            _cancellationTokenSource = new CancellationTokenSource();
            SendKeepAliveToNodes();
        }

        private void ResetTicks()
        {
            lock(_ticks)
            {
                _ticks.Clear();
                _groupManager.View.Others.ForEach(node => _ticks.Add(node));
                _logger.Warning(Tag.KeepAlive, $"Ticks resetted: Nodes {_ticks.ToString<Node>()}");
            }
        }

        public void Stop() 
        {
            _cancellationTokenSource?.Cancel();

            var jobPublisher = _groupManager.Topics.GetPublisher<KeepAlivePublisher>();
            jobPublisher.UnregisterForMessage(typeof(KeepAliveResponse), OnKeepAliveResponseReceived);
        }

        private void SendKeepAliveToNodes()
        {
            ResetTicks();

            _groupManager.View.Others.ForEach(node => 
            {
                try
                {
                    _groupManager.Send(node, new KeepAliveRequest(), Communication.NetworkManager.SendFailureStrategy.Reconnect).Wait();
                }
                catch(NotDeliveredException) { }
                _logger.Log(Tag.KeepAlive, $"Sent keep-alive request to {node}");
            });

            Task.Delay(KeepAliveManager.CoordinatorResponseWindow, _cancellationTokenSource.Token)
                .ContinueWith(t => { if (!t.IsCanceled) TimeoutFinished(); });

            Task.Delay(KeepAliveManager.CoordinatorRequestSendTimeout, _cancellationTokenSource.Token)
                .ContinueWith(t => { if (!t.IsCanceled) SendKeepAliveToNodes(); });
        }

        private void OnKeepAliveResponseReceived(Node node, Message message)
        { 
            lock(_ticks)
            {
                if (_ticks.Contains(node))
                {
                    _ticks.Remove(node);
                    _logger.Log(Tag.KeepAlive, $"Received keep-alive response from {node}");
                    _logger.Log(Tag.KeepAlive, $"After receive keep-alive from {node.ToString()} remains are {_ticks.ToString<Node>()}");
                }
                else
                {
                    _logger.Warning(Tag.KeepAlive, $"Received keep-alive response from {node} that's not in view");
                }
            }
        }

        private void TimeoutFinished()
        {
            lock(_ticks)
            {
                if (_ticks.Count > 0)
                {
                    _logger.Warning(Tag.KeepAlive, $"Nodes {_ticks.ToString<Node>()} died");
                    NodesDied?.Invoke(_ticks);
                    this.Stop();
                }
            }
        }
    }
}