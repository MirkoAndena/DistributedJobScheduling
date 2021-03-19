using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    public class TemporaryAckMessage : Message
    {
        public int OriginalSenderID { get; private set; }
        public int OriginalTimestamp { get; private set; }

        public TemporaryAckMessage(TemporaryMessage receivedMessage) : base()
        {
            OriginalSenderID = receivedMessage.SenderID.Value; //AckMessages need to have a sender and TimeStamp
            OriginalTimestamp = receivedMessage.TimeStamp.Value;
        }
    }
}