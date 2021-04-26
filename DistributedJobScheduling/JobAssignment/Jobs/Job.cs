using System.Runtime.InteropServices;
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
        public int ID { get; private set; }
        public int Node { get; private set; }
        public IJobResult Result { get; set; }
        public IJobWork Work { get; private set; }

        public Job(int id, int node, IJobWork work)
        {
            this.Status = JobStatus.PENDING;
            this.ID = id;
            this.Node = node;
            this.Result = null;
            this.Work = work;
        }

        private Job(int id, int node, IJobWork work, JobStatus status, IJobResult result)
        {
            this.Status = status;
            this.ID = id;
            this.Node = node;
            this.Result = result;
            this.Work = work;
        }

        public Task<IJobResult> Run() => Work.Run();

        public Job Clone() => new Job(ID, Node, Work, Status, Result);

        public void Update(Job job)
        {
            Status = job.Status;
            Result = job.Result;
        }

        public override string ToString() 
        {
            string result = Result == null ? "null" : Result.GetType().Name;
            return $"ID: {ID}, NODE: {Node}, STATUS: {Status.ToString()}, RESULT: {result}";
        }
    }
}