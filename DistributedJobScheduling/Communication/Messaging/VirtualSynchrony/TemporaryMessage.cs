using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class TemporaryMessage : Message
    {
        public bool IsMulticast { get; private set; }
        public Message UnstablePayload { get; private set; }

        //We don't need the timestamper
        public TemporaryMessage(bool isMulticast, Message unstablePayload) : base()
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