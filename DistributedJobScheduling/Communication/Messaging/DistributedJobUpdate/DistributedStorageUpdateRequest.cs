using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// Worker to Coordinator, update storage with this Job
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class DistributedStorageUpdateRequest : Message
    {
        private Job _job;
        public Job Job => _job;

        public DistributedStorageUpdateRequest(Job job) : base()
        {
            _job = job;
        }
    }
}