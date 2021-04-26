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
    using Storage = BlockingDictionarySecureStore<Dictionary<int, ClientJob>, int, ClientJob>;

    public class ClientStore : IInitializable, IClientStore
    {
        private Storage _store;
        private ILogger _logger;

        public ClientStore() : this(
            DependencyInjection.DependencyManager.Get<IStore<Dictionary<int, ClientJob>>>(),
            DependencyInjection.DependencyManager.Get<ILogger>())
        { }

        public ClientStore(IStore<Dictionary<int, ClientJob>> store, ILogger logger)
        {
            _store = new Storage(store);
            _logger = logger;
        }

        public void Init() => _store.Init();

        public void StoreClientJob(ClientJob job)
        {
            // Remove job with same ID
            _store.RemoveAll(current => current.ID == job.ID);
            _store.Add(job.ID, job);
            _store.ValuesChanged?.Invoke();
        }

        public void UpdateClientJobResult(int jobId, IJobResult result)
        {
            _logger.Log(Tag.JobStorage, $"Updating job {jobId} with result {result?.ToString()}");
            _store.ExecuteTransaction(jobs =>
            {
                foreach (ClientJob job in jobs.Values)
                    if (job.ID == jobId)
                    {
                        job.Result = result;
                        _store.ValuesChanged?.Invoke();
                        break;
                    }
            });
        }

        public List<ClientJob> ClientJobs(Predicate<IJobResult> predicate) => _store.GetAll(job => predicate.Invoke(job.Result));

        public List<IJobResult> Results(Predicate<int> predicate)
        {
            List<IJobResult> jobs = new List<IJobResult>();
            _store.Values.ForEach(job =>
            {
                if (predicate.Invoke(job.ID))
                    jobs.Add(job.Result);
            });
            return jobs;
        }

        public ClientJob Get(int id) => _store.Get(id);
    }
}