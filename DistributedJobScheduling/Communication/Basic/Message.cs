using System.Text;
using DistributedJobScheduling.Communication.Messaging;
using Newtonsoft.Json;
using static DistributedJobScheduling.Communication.Basic.Node;

namespace DistributedJobScheduling.Communication.Basic
{
    /// <summary>
    /// Implement the Scalar Clock
    /// </summary>
    public abstract class Message
    {
        public int TimeStamp => _messageID;
        private int _messageID;
        private int _isResponseOf;

        public int? SenderID;
        public int? ReceiverID;

        public Message(ITimeStamper timestampMechanism = null)
        {
            timestampMechanism ??= new ScalarTimeStamper();
            _messageID = timestampMechanism.CreateTimeStamp();
        }

        /// <summary>
        /// Create a message that is the response of another message
        /// </summary>
        public Message(Message message, ITimeStamper timestampMechanism = null) : this(timestampMechanism)
        {
            _isResponseOf = message._messageID;
        }

        private bool NodesInfoPresent => SenderID.HasValue && ReceiverID.HasValue;

        public bool IsTheExpectedMessage(Message previous)
        {
            bool idCheck = _isResponseOf == previous._messageID;
            bool nodeCheckEnabled = this.NodesInfoPresent && previous.NodesInfoPresent;
            return idCheck && nodeCheckEnabled ? SenderID.Value == previous.ReceiverID.Value && ReceiverID.Value == previous.SenderID.Value : true;
        }

        public byte[] Serialize() => JsonSerialization.Serialize(this);

        public static T Deserialize<T>(byte[] bytes) where T: Message => JsonSerialization.Deserialize<T>(bytes); 

        /// <summary>
        /// Binds message to this one (copies the IDs)
        /// </summary>
        /// <param name="message"></param>
        public void Bind(Message message)
        {
            message._messageID = _messageID;
            message._isResponseOf = _isResponseOf;
        }

        /// <summary>
        /// Binds the message node references to nodes from a specific node registry
        /// </summary>
        public virtual void BindToRegistry(INodeRegistry registry) {}
    }
}