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

    public interface IJobResult { }

    public abstract class Job
    {
        protected JobStatus _status;
        protected int? _id;
        protected int? _node;

        public JobStatus Status => _status;
        public int ID { get { return _id.Value; } set { _id = value; } }
        public int Node { get { return _node.Value; } set { _node = value; } }

        public bool IsAssigned => _node.HasValue;

        // ? The constructor will be called only by the client??
        protected Job()
        {
            _status = JobStatus.PENDING;
            _id = null;
            _node = null;
        }

        public abstract Task<IJobResult> Run();
    }
}