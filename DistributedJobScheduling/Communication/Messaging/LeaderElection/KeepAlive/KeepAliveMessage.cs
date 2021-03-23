using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive
{
    /// <summary>
    /// Message from node to others for Keep Alive 
    /// </summary>
    public class KeepAliveRequest : Message
    {
        [JsonConstructor]
        public KeepAliveRequest() : base() {}
    }

    public class KeepAliveResponse : Message
    {
        [JsonConstructor]
        public KeepAliveResponse(KeepAliveRequest request) : base(request) { }
    }
}