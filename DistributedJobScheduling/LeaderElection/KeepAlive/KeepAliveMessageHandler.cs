namespace DistributedJobScheduling.LeaderElection.KeepAlive
{
    public class KeepAliveMessageHandler
    {
        public Action<Node> OnNodeDied;
    }
}