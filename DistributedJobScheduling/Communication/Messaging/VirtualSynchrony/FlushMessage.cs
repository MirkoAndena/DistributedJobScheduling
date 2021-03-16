using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    public class FlushMessage : Message 
    {
        public ViewChangeMessage.ViewChangeOperation RelatedChangeOperation { get; private set; }
        public Node RelatedChangeNode { get; private set; }
        
        public FlushMessage(Node node, ViewChangeMessage.ViewChangeOperation viewChange, ITimeStamper timestampMechanism = null) : base(timestampMechanism) 
        {
            RelatedChangeNode = node;
            RelatedChangeOperation = viewChange;
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            RelatedChangeNode = registry.GetOrCreate(RelatedChangeNode);
        }
    }
}