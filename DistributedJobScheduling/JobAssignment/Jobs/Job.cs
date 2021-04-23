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

    public interface IJobWork
    {
        Task<IJobResult> Run();
    }

    [Serializable]
    public class Job
    {
        public JobStatus Status { get; set; }
        public int ID { get; set; }
        public int Node { get; set; }
        public IJobResult Result { get; set; }
        private IJobWork _work;

        public Job(int id, int node, IJobWork work)
        {
            this.Status = JobStatus.PENDING;
            this.ID = id;
            this.Node = node;
            this.Result = null;
        }

        public Task<IJobResult> Run() => _work.Run();

        public override string ToString() 
        {
            string result = Result == null ? "null" : Result.GetType().Name;
            return $"ID: {ID}, NODE: {Node}, STATUS: {Status.ToString()}, RESULT: {result}";
        }
    }
}