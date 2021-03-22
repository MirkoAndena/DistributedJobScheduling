using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging.LeaderElection
{
    /// <summary>
    /// Elect message sent from coordinator candidate to others
    /// </summary>
    public class ElectMessage : Message
    {
        public int ID { get; private set; }

        public ElectMessage(int id) : base()
        {
            ID = id;
        }
    }
}