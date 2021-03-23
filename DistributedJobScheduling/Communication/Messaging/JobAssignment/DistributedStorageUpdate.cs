using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// Coordinator to Worker, update storage with this Job
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    public class DistributedStorageUpdate : Message
    {
        private Job _job;
        public Job Job => _job;

        public DistributedStorageUpdate(Job job) : base()
        {
            _job = job;
        }
    }
}