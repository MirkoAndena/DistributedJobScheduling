using System.Threading;
using System;
using System.Threading.Tasks;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Storage
{
    public interface IJobStorage
    {
        event Action<Job> JobUpdated;
        event Action<Job> JobCreated;
        Task UpdateStatus(int id, JobStatus status);
        Task UpdateResult(int id, IJobResult result);
        Job Get(int jobID);
        int CreateJob(IJobWork jobWork);
        void CommitUpdate(Job job);
        Task<Job> FindJobToExecute(CancellationToken token);
    }
}