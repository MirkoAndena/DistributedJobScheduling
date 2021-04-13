using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class JobSyncMessage : Message
    {
        public List<Job> Jobs { get; private set; }

        public JobSyncMessage(List<Job> jobs) : base()
        {
            Jobs = jobs;
        }
    }
}