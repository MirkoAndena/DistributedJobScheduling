using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// InterfaceExecutor to Coordinator, insert request for a job
    /// </summary>
    [Serializable]
    public class InsertionRequest : Message
    {
        public IJobWork JobWork { get; private set; }
        public int RequestID { get; private set; }

        [JsonConstructor]
        public InsertionRequest(IJobWork jobWork, int requestID) : base()
        {
            this.JobWork = jobWork;
            this.RequestID = requestID;
        }
    }

    /// <summary>
    ///  Coordinator to InterfaceExecutor, insert response for a job insertion
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class InsertionResponse : Message
    {
        public int JobID { get; private set; }
        public int RequestID { get; private set; }

        public InsertionResponse(InsertionRequest request, int jobID, int requestID) : base(request)
        {
            this.JobID = jobID;
            this.RequestID = requestID;
        }
    }
}