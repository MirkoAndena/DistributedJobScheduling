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
        public bool Flushed { get; private set; }
        
        [JsonConstructor]
        public FlushMessage(ViewChange relatedChange, bool flushed, int viewId) : base(viewId) 
        {
            RelatedChange = relatedChange;
            Flushed = flushed;
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            RelatedChange.BindToRegistry(registry);
        }
    }
}