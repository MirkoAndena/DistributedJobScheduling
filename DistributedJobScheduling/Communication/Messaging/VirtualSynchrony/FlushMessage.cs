using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class FlushMessage : Message 
    {
        public ViewChangeMessage.ViewChangeOperation RelatedChangeOperation { get; private set; }
        public Node RelatedChangeNode { get; private set; }
        
        [JsonConstructor]
        public FlushMessage(Node relatedChangeNode, ViewChangeMessage.ViewChangeOperation relatedChangeOperation) : base() 
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