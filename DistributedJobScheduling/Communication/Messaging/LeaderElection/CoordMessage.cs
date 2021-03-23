using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.LeaderElection
{
    /// <summary>
    /// Coord message sent from coordinator (elected) to others
    /// </summary>
    public class CoordMessage : Message
    {
        public Node Coordinator { get; private set; }

        [JsonConstructor]
        public CoordMessage(Node me) : base()
        {
            Coordinator = me;
        }
    }
}