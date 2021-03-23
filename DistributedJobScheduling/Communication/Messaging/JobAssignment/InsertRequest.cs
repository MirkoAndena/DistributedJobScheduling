using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// InterfaceExecutor to Coordinator, insert request for a job
    /// </summary>
    public class InsertionRequest : Message
    {
        private Job _job;
        private int _requestID;
        public Job Job => _job;
        public int RequestID => _requestID;

        [JsonConstructor]
        public InsertionRequest(Job job, int requestID) : base()
        {
            _job = job;
            _requestID = requestID;
        }
    }

    /// <summary>
    ///  Coordinator to InterfaceExecutor, insert response for a job insertion
    /// </summary>
    public class InsertionResponse : Message
    {
        private int _jobID;
        private int _requestID;
        public int JobID => _jobID;
        public int RequestID => _requestID;

        [JsonConstructor]
        public InsertionResponse(InsertionRequest request, int jobID, int requestID) : base(request)
        {
            _jobID = jobID;
            _requestID = requestID;
        }
    }
}