using System;
using System.Text;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Configuration;
using Newtonsoft.Json;
using static DistributedJobScheduling.Communication.Basic.Node;

//TODO: Byte serialize instead of json
namespace DistributedJobScheduling.Communication.Basic
{
    /// <summary>
    /// Implement the Scalar Clock
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public abstract class Message : IRegistryBindable
    {
        public int? TimeStamp => _messageID;
        private int? _messageID;
        private int? _isResponseOf;

        public int SenderID { get; private set; }
        public int? ReceiverID;

        public Message()
        {
            var configurationService = DependencyInjection.DependencyManager.Get<IConfigurationService>();
            this.SenderID = configurationService.GetValue<int>("nodeId", -1);
        }

        /// <summary>
        /// Create a message that is the response of another message
        /// </summary>
        public Message(Message message)
        {
            _isResponseOf = message._messageID;
        }

        private bool NodesInfoPresent => ReceiverID.HasValue;

        public bool IsTheExpectedMessage(Message previous)
        {
            bool idCheck = _isResponseOf == previous._messageID;
            bool nodeCheckEnabled = this.NodesInfoPresent && previous.NodesInfoPresent;
            if (nodeCheckEnabled)
                return idCheck && SenderID == previous.ReceiverID.Value && ReceiverID.Value == previous.SenderID;
            else
                return idCheck;
        }

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

        public virtual Message ApplyStamp(ITimeStamper timeStamper)
        {
            if(this._messageID != null)
                throw new System.Exception("Message already timestamped!");
                
            lock(timeStamper)
            {
                this._messageID = timeStamper.CreateTimeStamp();
            }
            return this;
        }
    }
}