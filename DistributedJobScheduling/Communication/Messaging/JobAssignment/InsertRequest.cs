using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// InterfaceExecutor to Coordinator, insert request for a job
    /// </summary>
    public class InsertionRequest : Message
    {
        private Job _job;

        public InsertionRequest(Job job) : base()
        {
            _job = job;
        }
    }

    /// <summary>
    ///  Coordinator to InterfaceExecutor, insert response for a job insertion
    /// </summary>
    public class InsertionResponse : Message
    {
        private int _jobID;

        public InsertionResponse(InsertionRequest request, int jobID) : base(request)
        {
            _jobID = jobID;
        }
    }
}