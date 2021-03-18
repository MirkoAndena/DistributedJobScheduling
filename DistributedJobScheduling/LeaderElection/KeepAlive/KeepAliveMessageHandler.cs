using System;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.LeaderElection.KeepAlive
{
    public class KeepAliveMessageHandler
    {
        public Action<Node> OnNodeDied;
    }
}