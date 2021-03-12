using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// Client to InterfaceExecutor, request for a job execution
    /// </summary>
    public class ExecutionRequest : Message
    {
        private Job _job;
        public Job Job => _job;

        public ExecutionRequest(Job job) : base()
        {
            _job = job;
        }
    }

    /// <summary>
    /// InterfaceExecutor to Client, response for a job execution
    /// </summary>
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