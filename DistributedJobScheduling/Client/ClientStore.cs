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

namespace DistributedJobScheduling.Client
{
    public class ClientJob
    {
        public int ID;
        public IJobResult Result;

        public ClientJob(int id)
        {
            ID = id;
        }
    }

    public class Storage
    {
        public Node Worker;

        public List<ClientJob> Jobs;
    }

    public class ClientStore : IInitializable
    {
        private SecureStore<Storage> _store;
        private ILogger _logger;

        public ClientStore() : this(
            DependencyInjection.DependencyManager.Get<IStore<Storage>>(), 
            DependencyInjection.DependencyManager.Get<ILogger>()) { }

        public ClientStore(IStore<Storage> store, ILogger logger)
        {
            _store = new SecureStore<Storage>(store);
            _logger = logger;
        }

        public void Init() => _store.Init();

        public bool IsWorkerPresent => _store.Value.Worker != null;

        public Node GetWorker() => _store.Value.Worker;

        public void StoreWorker(Node node)
        {
            _store.Value.Worker = node;
            _store.ValuesChanged?.Invoke();
        }

        public void StoreClientJob(ClientJob job)
        {
            _store.Value.Jobs.Add(job);
            _store.ValuesChanged?.Invoke();
        }
    }
}