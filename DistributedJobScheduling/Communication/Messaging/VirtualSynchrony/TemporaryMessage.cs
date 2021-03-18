using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    public class TemporaryMessage : Message
    {
        public bool IsMulticast { get; private set; }
        public Message UnstablePayload { get; private set; }

        public TemporaryMessage(bool isMulticast, Message unstableMessage)
        {
            IsMulticast = isMulticast;
            UnstablePayload = unstableMessage;
            unstableMessage.Bind(this);
        }
    }
}