using System;
using System.Threading.Tasks;

namespace DistributedJobScheduling.JobAssignment.Jobs
{
    public enum JobStatus 
    {
        PENDING,
        RUNNING,
        COMPLETED,
        REMOVED
    }

    public interface IJobResult { }

    public abstract class Job
    {
        public JobStatus Status { get; set; }
        public int? ID { get; set; }
        public int? Node { get; set; }

        // ? The constructor will be called only by the client??
        protected Job()
        {
            Status = JobStatus.PENDING;
            ID = null;
            Node = null;
        }

        public abstract Task<IJobResult> Run();
    }
}