using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Communication.Messaging.JobAssignment
{
    /// <summary>
    /// Coordinator to each Executor, assignment for a job
    /// </summary>
    public class AssignmentMessage : Message
    {
        private Job _job;

        public AssignmentMessage(Job job) : base()
        {
            if (!job.ID.HasValue)
                throw new System.Exception("You must send a assigned job (ID not null)");
            _job = job;
        }
    }
}