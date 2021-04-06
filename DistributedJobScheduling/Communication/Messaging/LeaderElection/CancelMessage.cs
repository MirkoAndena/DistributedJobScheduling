using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.LeaderElection
{
    /// <summary>
    /// Cancel message sent from coordinator candidate to another candidate
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class CancelMessage : Message
    {
        public CancelMessage() : base()
        {
            
        }
    }
}