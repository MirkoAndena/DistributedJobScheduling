using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class TemporaryAckMessage : ViewMessage
    {
        public int OriginalSenderID { get; private set; }
        public int OriginalTimestamp { get; private set; }

        [JsonConstructor]
        public TemporaryAckMessage(int originalSenderID, int originalTimestamp, int viewId) : base(viewId) 
        { 
            OriginalSenderID = originalSenderID; //AckMessages need to have a sender and TimeStamp
            OriginalTimestamp = originalTimestamp;
        }
        public TemporaryAckMessage(TemporaryMessage receivedMessage) : this(receivedMessage.SenderID.Value, receivedMessage.TimeStamp.Value, receivedMessage.ViewId) {}
    }
}