using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive
{
    /// <summary>
    /// Message from node to others for Keep Alive 
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class KeepAliveRequest : Message
    {
        public KeepAliveRequest() : base() {}
    }

    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class KeepAliveResponse : Message
    {
        public KeepAliveResponse(KeepAliveRequest request) : base(request) { }
    }
}