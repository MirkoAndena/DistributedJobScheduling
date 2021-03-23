using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    public class TemporaryAckMessage : Message
    {
        public int OriginalSenderID { get; private set; }
        public int OriginalTimestamp { get; private set; }

        [JsonConstructor]
        public TemporaryAckMessage(int originalSenderID, int originalTimestamp) : base() 
        { 
            OriginalSenderID = originalSenderID; //AckMessages need to have a sender and TimeStamp
            OriginalTimestamp = originalTimestamp;
        }
        public TemporaryAckMessage(TemporaryMessage receivedMessage) : this(receivedMessage.SenderID.Value, receivedMessage.TimeStamp.Value) {}
    }
}