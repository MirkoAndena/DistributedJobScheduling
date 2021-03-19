using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive
{
    /// <summary>
    /// Message from node to others for Keep Alive 
    /// </summary>
    public class KeepAliveRequest : Message
    {
        public KeepAliveRequest(ITimeStamper timeStamper) : base(timeStamper) {}
    }

    public class KeepAliveResponse : Message
    {
        public KeepAliveResponse(KeepAliveRequest request, ITimeStamper timeStamper) : base(request, timeStamper) { }
    }
}