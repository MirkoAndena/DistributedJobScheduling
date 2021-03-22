using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Request a group to join, processed only by the coordinator
    /// </summary>
    public class ViewJoinRequest : Message
    {
        public Node JoiningNode { get; private set; }

        public ViewJoinRequest(Node node) : base() 
        {
            JoiningNode = node;
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            JoiningNode = registry.GetOrCreate(JoiningNode);
        }
    }
}