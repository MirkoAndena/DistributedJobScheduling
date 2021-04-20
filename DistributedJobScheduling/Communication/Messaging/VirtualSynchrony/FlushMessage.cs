using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class FlushMessage : ViewMessage 
    {
        public ViewChange RelatedChange { get; private set; }
        
        [JsonConstructor]
        public FlushMessage(ViewChange relatedChange, int viewId) : base(viewId) 
        {
            RelatedChange = relatedChange;
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            RelatedChange.BindToRegistry(registry);
        }
    }
}