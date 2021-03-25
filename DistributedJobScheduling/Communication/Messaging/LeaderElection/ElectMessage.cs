using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.LeaderElection
{
    /// <summary>
    /// Elect message sent from coordinator candidate to others
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ElectMessage : Message
    {
        public int ID { get; private set; }

        public ElectMessage(int id) : base()
        {
            ID = id;
        }
    }
}