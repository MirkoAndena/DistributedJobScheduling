using System;
using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Client
{
    public interface IClientStore
    {
        List<ClientJob> ClientJobs(Predicate<IJobResult> predicate);
        void Init();
        void StoreClientJob(ClientJob job);
        void UpdateClientJobResult(int jobId, IJobResult result);
    }
}