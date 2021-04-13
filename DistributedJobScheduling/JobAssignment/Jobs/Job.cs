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

    [Serializable]
    public abstract class Job
    {
        public JobStatus Status { get; set; }
        public int? ID { get; set; }
        public int? Node { get; set; }

        public IJobResult Result { get; set; }

        // ? The constructor will be called only by the client??
        protected Job()
        {
            Status = JobStatus.PENDING;
            ID = null;
            Node = null;
            Result = null;
        }

        public abstract Task<IJobResult> Run();

        public override string ToString() 
        {
            string id = ID.HasValue ? ID.Value.ToString() : "unknown";
            string node = Node.HasValue ? Node.Value.ToString() : "unknown";
            return $"ID: {id}, NODE: {node}, STATUS: {Status.ToString()}";
        }
    }
}