using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ViewMessage : Message
    {
        public int ViewId { get; private set; }
        
        public ViewMessage(int viewId) : base() {
            ViewId = viewId;
        }
    }
}