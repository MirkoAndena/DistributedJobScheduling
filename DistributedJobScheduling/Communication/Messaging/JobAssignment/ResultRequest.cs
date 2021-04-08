using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// Client to InterfaceExecutor, request for a job result
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ResultRequest : Message
    {
        public int RequestID;

        public ResultRequest(int requestID) : base()
        {
            RequestID = requestID;
        }
    }

    /// <summary>
    /// InterfaceExecutor to Client, response with the job result
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ResultResponse : Message
    {
        public IJobResult Result;
        public JobStatus Status;

        public ResultResponse(Job job) : base()
        {
            Result = job.Result;
            Status = job.Status;
        }
    }
}