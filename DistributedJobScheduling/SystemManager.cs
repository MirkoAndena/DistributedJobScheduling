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

        private const string ROOT = "./DataStore/";
        private string JOBS_PATH = $"{ROOT}/jobs.json";
        private string TRANSLATIONTABLE_PATH = $"{ROOT}/translationTable.json";

        #endregion

        public SystemManager()
        {
            RegisterSubSystem<IConfigurationService, DictConfigService>(new DictConfigService());
        }

        protected override void CreateSubsystems()
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            ByteSerializer byteSerializer = new ByteSerializer();

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