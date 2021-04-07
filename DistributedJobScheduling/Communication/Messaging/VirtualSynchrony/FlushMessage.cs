using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class FlushMessage : ViewMessage 
    {
        public ViewChangeOperation RelatedChangeOperation { get; private set; }
        public Node RelatedChangeNode { get; private set; }
        
        [JsonConstructor]
        public FlushMessage(Node relatedChangeNode, ViewChangeOperation relatedChangeOperation, int viewId) : base(viewId) 
        {
            RelatedChangeNode = relatedChangeNode;
            RelatedChangeOperation = relatedChangeOperation;
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            RelatedChangeNode = registry.GetOrCreate(RelatedChangeNode);
        }
    }
}