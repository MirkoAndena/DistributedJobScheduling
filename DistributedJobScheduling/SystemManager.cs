using System;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.LeaderElection;
using DistributedJobScheduling.LeaderElection.KeepAlive;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.VirtualSynchrony;
using static DistributedJobScheduling.Communication.Basic.Node;

namespace DistributedJobScheduling
{
    public class SystemManager : SystemLifeCycle
    {
        #region Paths

        private const string ROOT = "./AppData/";
        private string JOBS_PATH = $"{ROOT}/jobs.json";
        private string TRANSLATIONTABLE_PATH = $"{ROOT}/translationTable.json";

        #endregion

        public SystemManager()
        {
            RegisterSubSystem<IConfigurationService, DictConfigService>(new DictConfigService());
        }

        protected override bool CreateConfiguration(IConfigurationService configurationService, string[] args)
        {
            int id;
            bool isId = Int32.TryParse(args.Length > 0 ? args[0].Trim() : Environment.GetEnvironmentVariable("NODE_ID"), out id);
            bool coordinator = (args.Length > 1 && args[1].Trim().ToLower() == "coordinator") || (Environment.GetEnvironmentVariable("COORD") != null);
            
            if (!isId) return false;
            
            Console.WriteLine($"Configuration nodeId: {id}");
            configurationService.SetValue<int?>("nodeId", id);
            configurationService.SetValue<bool>("coordinator", coordinator);
            return true;
        }

        protected override void CreateSubsystems()
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            ByteBase64Serializer byteSerializer = new ByteBase64Serializer();

            RegisterSubSystem<INodeRegistry, NodeRegistryService>(new NodeRegistryService());
            RegisterSubSystem<ILogger, CsvLogger>(new CsvLogger(ROOT, separator: "|"));
            RegisterSubSystem<ITimeStamper, ScalarTimeStamper>(new ScalarTimeStamper());
            RegisterSubSystem<ICommunicationManager, NetworkManager>(new NetworkManager(jsonSerializer));
            RegisterSubSystem<IGroupViewManager, GroupViewManager>(new GroupViewManager());
            
            RegisterSubSystem<IStore<Jobs>, FileStore<Jobs>>(new FileStore<Jobs>(JOBS_PATH, jsonSerializer));
            JobManager jobManager = new JobManager();
            RegisterSubSystem<JobManager>(jobManager);
            RegisterSubSystem<IStore<Table>, FileStore<Table>>(new FileStore<Table>(TRANSLATIONTABLE_PATH, jsonSerializer));
            TranslationTable translationTable = new TranslationTable();
            RegisterSubSystem<TranslationTable>(translationTable);

            RegisterSubSystem<JobExecutor>(new JobExecutor(jobManager));
            RegisterSubSystem<JobMessageHandler>(new JobMessageHandler(jobManager, translationTable));
            RegisterSubSystem<KeepAliveManager>(new KeepAliveManager());
            RegisterSubSystem<BullyElectionMessageHandler>(new BullyElectionMessageHandler());
        }
    }
}