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
        public int ClientJobId;

        public ResultResponse(ResultRequest message, Job job, int clientJobId) : base(message)
        {
            Result = job.Result;
            Status = job.Status;
            ClientJobId = clientJobId;
        }
    }
}