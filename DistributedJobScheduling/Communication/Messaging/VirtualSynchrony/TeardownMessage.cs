using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Message sent to views that know are not the main one and should be dismantled
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class TeardownMessage : Message
    {
        [JsonConstructor]
        public TeardownMessage() : base()
        {
        }
    }
}