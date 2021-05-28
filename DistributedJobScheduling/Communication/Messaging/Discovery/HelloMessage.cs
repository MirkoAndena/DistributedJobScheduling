using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.Discovery
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class HelloMessage : Message { }
}