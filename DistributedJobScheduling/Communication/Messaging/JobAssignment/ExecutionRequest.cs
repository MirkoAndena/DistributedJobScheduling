using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// Client to InterfaceExecutor, request for a job execution
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ExecutionRequest : Message
    {
        public IJobWork JobWork { get; private set; }

        public ExecutionRequest(IJobWork job) : base()
        {
            this.JobWork = job;
        }
    }

    /// <summary>
    /// InterfaceExecutor to Client, response for a job execution
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ExecutionResponse : Message
    {
        private int _requestID;
        public int RequestID => _requestID;

        public ExecutionResponse(ExecutionRequest request, int requestID) : base(request)
        {
            _requestID = requestID;
        }
    }

    /// <summary>
    /// Client to InterfaceExecutor, acknowledge
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ExecutionAck : Message
    {
        private int _requestID;
        public int RequestID => _requestID;

        public ExecutionAck(ExecutionResponse response, int requestID) : base(response)
        {
            _requestID = requestID;
        }
    }
}