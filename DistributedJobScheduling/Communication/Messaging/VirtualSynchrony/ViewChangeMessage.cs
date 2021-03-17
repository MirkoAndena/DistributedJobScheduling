using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    public class ViewChangeMessage : Message
    {
        public enum ViewChangeOperation
        {
            Joined,
            Left
        }

        /// <value>Node that changed, WARNING: Don't use this instance to communicate use the NodeRegistry</value>
        public Node Node { get; private set; }
        public ViewChangeOperation ViewChange { get; private set; }

        public ViewChangeMessage(Node node, ViewChangeOperation viewChange, ITimeStamper timestampMechanism = null) : base(timestampMechanism) 
        {
            Node = node;
            ViewChange = viewChange;
        }

        public bool IsSame(ViewChangeMessage other) => IsSame(other.Node, other.ViewChange);
        public bool IsSame(Node node, ViewChangeOperation viewChange)
        {
            return Node.ID == node.ID && ViewChange == viewChange;
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            Node = registry.GetOrCreate(Node);
        }
    }
}