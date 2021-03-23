using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    public class ViewChangeMessage : Message
    {
        public class ViewChange
        {
            /// <value>Node that changed, WARNING: Don't use this instance to communicate use the NodeRegistry</value>
            public Node Node { get; set; }
            public ViewChangeOperation Operation { get; set; }
            
            public bool IsSame(ViewChange other) => IsSame(other.Node, other.Operation);
            public bool IsSame(Node node, ViewChangeOperation operation)
            {
                return Node.ID == node.ID && Operation == operation;
            }

            public void BindToRegistry(Node.INodeRegistry registry)
            {
                Node = registry.GetOrCreate(Node);
            }
        }

        public enum ViewChangeOperation
        {
            Joined,
            Left
        }

        public ViewChange Change { get; private set; }

        public ViewChangeMessage(Node node, ViewChangeOperation operation) : base() 
        {
            Change = new ViewChange
            {
                Node = node,
                Operation = operation
            };
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            Change.BindToRegistry(registry);
        }
    }
}