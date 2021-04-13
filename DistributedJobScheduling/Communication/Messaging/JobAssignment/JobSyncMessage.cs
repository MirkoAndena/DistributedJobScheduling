using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class JobSyncMessage : Message
    {
        public JobSyncMessage() : base()
        {
        }
    }
}