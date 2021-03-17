using System.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.LeaderElection
{
    public class BullyElectionCandidate
    {
        // If no-one responds to ELECT for {timeout} seconds ...
        const int timeout = 5;
        private IGroupViewManager _group; 
        private CancellationTokenSource _cancellationTokenSource;
        public Action<List<Node>> SendElect, SendCoords;
        private ILogger _logger;

        public BullyElectionCandidate() : this (DependencyInjection.DependencyManager.Get<IGroupViewManager>()) { }
        public BullyElectionCandidate(IGroupViewManager group)
        {
            _group = group;
        }

        public void Run(Node died = null)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            List<Node> nodesWithIdHigherThanMe = NodesWithId(id => id > _group.View.Me.ID.Value && died != null && id != died.ID.Value);
            SendElect?.Invoke(nodesWithIdHigherThanMe);
            Task.Delay(TimeSpan.FromSeconds(timeout), _cancellationTokenSource.Token).ContinueWith(t => 
            {
                if (t.IsCompleted)
                {
                    _logger.Log(Tag.LeaderElection, "Response window closed with no refuse, i'm the leader");
                    SendImTheLeaderNow(died);
                }
                else
                    _logger.Log(Tag.LeaderElection, "Election stopped because someone refuse");
            });
        }

        private List<Node> NodesWithId(Predicate<int> idCondition)
        {
            List<Node> nodes = new List<Node>();
            _group.View.Others.ForEach(node =>
            {
                if (node.ID.HasValue && idCondition.Invoke(node.ID.Value))
                    nodes.Add(node);
            });
            return nodes;
        }

        private void SendImTheLeaderNow(Node died)
        {
            List<Node> nodesWithIdLowerThanMe = NodesWithId(id => id < _group.View.Me.ID.Value && died != null && id != died.ID.Value);
            SendCoords?.Invoke(nodesWithIdLowerThanMe);
        }

        public void CancelElection() => _cancellationTokenSource.Cancel();
    }
}