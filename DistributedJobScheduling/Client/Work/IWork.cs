using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Client.Work
{
    public interface IWork
    {
        List<Job> CreateJobs();
        void ComputeResult(List<IJobResult> results, string directory);
    }
}