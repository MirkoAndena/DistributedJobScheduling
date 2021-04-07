using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class TemporaryMessage : ViewMessage
    {
        public bool IsMulticast { get; private set; }
        public Message UnstablePayload { get; private set; }

        //We don't need the timestamper
        public TemporaryMessage(bool isMulticast, Message unstablePayload, int viewId) : base(viewId)
        {
            IsMulticast = isMulticast;
            UnstablePayload = unstablePayload;
            UnstablePayload.Bind(this);
        }

        public override Message ApplyStamp(ITimeStamper timeStamper)
        {
            UnstablePayload.ApplyStamp(timeStamper);
            UnstablePayload.Bind(this);
            return this;
        }
    }
}