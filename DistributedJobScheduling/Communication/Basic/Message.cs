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
        private string _messageID;
        private string _isResponseOf;

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

        public bool IsTheExpectedMessage(Message previous) => _isResponseOf == previous._messageID;

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