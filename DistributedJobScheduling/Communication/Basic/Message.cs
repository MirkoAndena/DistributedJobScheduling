using System.Text;
using DistributedJobScheduling.Communication.Messaging;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Basic
{
    /// <summary>
    /// Implement the Scalar Clock
    /// </summary>
    public abstract class Message
    {
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

        public bool NodesInfoPresent => SenderID.HasValue && ReceiverID.HasValue;

        public bool IsTheExpectedMessage(Message previous)
        {
            bool idCheck = _isResponseOf == previous._messageID;
            bool nodeCheckEnabled = this.NodesInfoPresent && previous.NodesInfoPresent;
            return idCheck && nodeCheckEnabled ? SenderID.Value == previous.ReceiverID.Value && ReceiverID.Value == previous.SenderID.Value : true;
        }

        public byte[] Serialize()
        {
            string json = JsonConvert.SerializeObject(this);
            return Encoding.UTF8.GetBytes(json);
        }

        public static T Deserialize<T>(byte[] bytes) where T: Message
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}