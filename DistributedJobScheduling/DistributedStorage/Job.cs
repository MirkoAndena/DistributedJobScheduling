using System.Threading.Tasks;

namespace DistributedJobScheduling.DistributedStorage
{
    public enum JobStatus 
    {
        PENDING,
        RUNNING,
        COMPLETED,
        REMOVED
    }

    public abstract class Job<T>
    {
        protected JobStatus _status;
        protected int? _id;
        protected int? _node;

        // ? The constructor will be called only by the client??
        protected Job()
        {
            _status = JobStatus.PENDING;
            _id = null;
            _node = null;
        }

        public abstract Task<T> Run();
    }
}