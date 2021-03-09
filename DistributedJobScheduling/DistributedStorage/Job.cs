using System;
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

        public Action<Job> OnStatusChanged;

        public JobStatus Status => _status;
        public int ID { get { return _id.Value; } set { _id = value; } }
        public int Node { get { return _node.Value; } set { _node = value; } }

        // ? The constructor will be called only by the client??
        protected Job()
        {
            _status = JobStatus.PENDING;
            _id = null;
            _node = null;
        }

        private void ChangeStatus(JobStatus jobStatus)
        {
            _status = jobStatus;
            OnStatusChanged?.Invoke(this);
        }

        public async Task<IJobResult> Run()
        {
            ChangeStatus(JobStatus.RUNNING);
            IJobResult result = await _run();
            ChangeStatus(JobStatus.COMPLETED);
            return result;
        }

        public abstract Task<IJobResult> _run();

        public void SetRemoved() => ChangeStatus(JobStatus.REMOVED);
    }
}