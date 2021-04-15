using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.JobAssignment.Jobs;
using System;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Client
{
    using Storage = BlockingListSecureStore<List<ClientJob>, ClientJob>;

    public class ClientStore : IInitializable, IClientStore
    {
        private Storage _store;
        private ILogger _logger;

        public ClientStore() : this(
            DependencyInjection.DependencyManager.Get<IStore<List<ClientJob>>>(),
            DependencyInjection.DependencyManager.Get<ILogger>())
        { }

        public ClientStore(IStore<List<ClientJob>> store, ILogger logger)
        {
            _store = new Storage(store);
            _logger = logger;
        }

        public void Init() => _store.Init();

        public void StoreClientJob(ClientJob job)
        {
            // Remove job with same ID
            _store.RemoveAll(current => current.ID == job.ID);
            _store.Add(job);
            _store.ValuesChanged?.Invoke();
        }

        public void UpdateClientJobResult(int jobId, IJobResult result)
        {
            _logger.Log(Tag.JobStorage, $"Updating job {jobId} with result {result?.ToString()}");
            _store.ExecuteTransaction(jobs =>
            {
                foreach (ClientJob job in jobs)
                    if (job.ID == jobId)
                    {
                        job.Result = result;
                        _store.ValuesChanged?.Invoke();
                        break;
                    }
            });
        }

        public List<ClientJob> ClientJobs(Predicate<IJobResult> predicate)
        {
            List<ClientJob> jobs = new List<ClientJob>();
            _store.ForEach(job =>
            {
                if (predicate.Invoke(job.Result))
                    jobs.Add(job);
            });
            return jobs;
        }
    }
}