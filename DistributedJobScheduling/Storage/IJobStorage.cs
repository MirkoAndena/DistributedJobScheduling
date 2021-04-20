using System.Threading;
using System;
using System.Threading.Tasks;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Storage
{
    public interface IJobStorage
    {
        event Action<Job> JobUpdated;
        void UpdateJob(Job job);
        void SetJobDeliveredToClient(Job job);
        Job Get(int jobID);
        void InsertAndAssign(Job job);
        void InsertOrUpdateJobLocally(Job job);
        Task<Job> FindJobToExecute(CancellationToken token);
    }
}