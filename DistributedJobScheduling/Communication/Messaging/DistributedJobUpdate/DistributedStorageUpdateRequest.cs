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
        public Job Job { get; private set; }

        public DistributedStorageUpdateRequest(Job job) : base()
        {
            this.Job = job;
        }

        public override string ToString() => $"StorageUpdateRequest with {Job.ToString()}";
    }
}