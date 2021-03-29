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
    public class Requests
    {
        public Node Worker;
        public Jobs Jobs;
    }

    public class JobRequests : IInitializable
    {
        private SecureStore<Requests> _store;
        private ILogger _logger;

        public JobRequests() : this(
            DependencyInjection.DependencyManager.Get<IStore<Requests>>(), 
            DependencyInjection.DependencyManager.Get<ILogger>()) { }

        public JobRequests(IStore<Requests> store, ILogger logger)
        {
            _store = new SecureStore<Requests>(store);
            _logger = logger;
        }

        public void Init() => _store.Init();

        public Node GetAWorker() => _store.Value.Worker;

        public void Store(Job job)
        {
            _store.Value.Jobs.List.Add(job);
            _store.ValuesChanged?.Invoke();
        }
    }
}