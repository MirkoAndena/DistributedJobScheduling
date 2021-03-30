using System;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.LeaderElection;
using DistributedJobScheduling.LeaderElection.KeepAlive;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.VirtualSynchrony;
using static DistributedJobScheduling.Communication.Basic.Node;

namespace DistributedJobScheduling.Client
{
    public class ClientSystemManager : SystemLifeCycle
    {
        #region Paths

        private const string ROOT = "./AppDataClient/";
        private string STORAGE_PATH = $"{ROOT}/storage.json";

        #endregion

        private JobMessageHandler _messageHandler;

        public ClientSystemManager()
        {
            RegisterSubSystem<IConfigurationService, DictConfigService>(new DictConfigService());
        }

        protected override bool CreateConfiguration(IConfigurationService configurationService, string[] args)
        {
            bool client = (args.Length > 0 && args[0].Trim().ToLower() == "client") || Environment.GetEnvironmentVariable("CLIENT") == "true";
            configurationService.SetValue<bool>("client", client);
            return client;
        }

        protected override void CreateSubsystems()
        {
            RegisterSubSystem<ISerializer, JsonSerializer>(new JsonSerializer());
            RegisterSubSystem<INodeRegistry, NodeRegistryService>(new NodeRegistryService());
            RegisterSubSystem<ILogger, CsvLogger>(new CsvLogger(ROOT, separator: "|"));
            RegisterSubSystem<IStore<Storage>, FileStore<Storage>>(new FileStore<Storage>(STORAGE_PATH));
            
            ClientStore store = new ClientStore();
            RegisterSubSystem<ClientStore>(store);

            WorkerSearcher workerSearcher = new WorkerSearcher(store);
            workerSearcher.WorkerFound += CreateAndRequestAssignment;
            RegisterSubSystem<WorkerSearcher>(workerSearcher);

            _messageHandler = new JobMessageHandler(store);
        }

        private void CreateAndRequestAssignment(Node worker)
        {
            Job job = new TimeoutJob(5);
            _messageHandler.SubmitJob(worker, job);
        }
    }
}