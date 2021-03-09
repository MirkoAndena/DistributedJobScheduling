using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    public class TemporaryMessage : Message
    {
        public Message UnstablePayload { get; private set; }

        public TemporaryMessage(Message unstableMessage)
        {
            UnstablePayload = unstableMessage;
            unstableMessage.Bind(this);
        }
    }
}