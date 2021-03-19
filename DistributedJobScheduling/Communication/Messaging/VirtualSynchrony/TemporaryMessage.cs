using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    public class TemporaryMessage : Message
    {
        public bool IsMulticast { get; private set; }
        public Message UnstablePayload { get; private set; }

        //We don't need the timestamper
        public TemporaryMessage(bool isMulticast, Message unstableMessage) : base(null)
        {
            IsMulticast = isMulticast;
            UnstablePayload = unstableMessage;
            unstableMessage.Bind(this);
        }
    }
}