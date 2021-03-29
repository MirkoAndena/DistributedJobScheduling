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

namespace DistributedJobScheduling.Client
{
    public class ClientSystemManager : SystemLifeCycle
    {
        #region Paths

        private const string ROOT = "./AppDataClient/";
        private string JOBS_PATH = $"{ROOT}/jobs.json";

        #endregion

        public ClientSystemManager()
        {
            RegisterSubSystem<IConfigurationService, DictConfigService>(new DictConfigService());
        }

        protected override bool CreateConfiguration(IConfigurationService configurationService, string[] args)
        {
            bool client = args.Length > 0 && args[0].Trim().ToLower() == "client";
            configurationService.SetValue<bool>("client", client);
            return true;
        }

        protected override void CreateSubsystems()
        {
            JsonSerializer jsonSerializer = new JsonSerializer();
            ByteBase64Serializer byteSerializer = new ByteBase64Serializer();

            RegisterSubSystem<INodeRegistry, NodeRegistryService>(new NodeRegistryService());
            RegisterSubSystem<ILogger, CsvLogger>(new CsvLogger(ROOT, separator: "|"));
        }
    }
}