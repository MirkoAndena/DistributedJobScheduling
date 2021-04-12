using System.Threading.Tasks;
using System.Security.Principal;
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
using DistributedJobScheduling.Communication.Basic.Speakers;

namespace DistributedJobScheduling.Client
{
    public class ClientSystemManager : SystemLifeCycle
    {
        #region Paths

        private const string ROOT = "./AppDataClient/";
        private string STORAGE_PATH = $"{ROOT}/storage.json";

        #endregion

        private JobInsertionMessageHandler _messageHandler;
        private JobResultMessageHandler _jobResultHandler;

        public ClientSystemManager()
        {
            RegisterSubSystem<IConfigurationService, DictConfigService>(new DictConfigService());
        }

        protected override void CreateConfiguration(IConfigurationService configurationService, string[] args)
        {
            bool client = (args.Length > 0 && args[0].Trim().ToLower() == "client") || Environment.GetEnvironmentVariable("CLIENT") == "true";
        
            // Read worker ip
            string envIp = Environment.GetEnvironmentVariable("WORKER");
            string ip = null;

            if (args.Length > 1)
                ip = args[1];
            else if (envIp != null)
                ip = envIp;

            bool correctIp = ip != null && NetworkUtils.IsAnIp(ip);
            if (!correctIp) throw new Exception("worker (remote) ip not valid");

            var now = DateTime.Now;
            var id = now.Millisecond + now.Second << 4 + now.Minute << 8; // funzione a caso per generare un numero pseudo-univoco

            configurationService.SetValue<int>("id", id);
            configurationService.SetValue<bool>("client", client);
            configurationService.SetValue<string>("worker", ip);
        }

        protected override void CreateSubsystems()
        {
            RegisterSubSystem<ISerializer, JsonSerializer>(new JsonSerializer());
            RegisterSubSystem<INodeRegistry, NodeRegistryService>(new NodeRegistryService());
            RegisterSubSystem<ILogger, CsvLogger>(new CsvLogger(ROOT, separator: "|"));
            RegisterSubSystem<ITimeStamper, ScalarTimeStamper>(new ScalarTimeStamper());
            RegisterSubSystem<IStore<Storage>, FileStore<Storage>>(new FileStore<Storage>(STORAGE_PATH));
            
            ClientStore store = new ClientStore();
            RegisterSubSystem<ClientStore>(store);

            _messageHandler = new JobInsertionMessageHandler(store);
            RegisterSubSystem<JobInsertionMessageHandler>(_messageHandler);
            _jobResultHandler = new JobResultMessageHandler(store);
            RegisterSubSystem<JobResultMessageHandler>(_jobResultHandler);
        }

        protected override void OnSystemStarted() => Main();

        private void Main()
        {
            var nodeRegistry = DependencyInjection.DependencyManager.Get<INodeRegistry>();
            var configuration = DependencyInjection.DependencyManager.Get<IConfigurationService>();
            var serializer = DependencyInjection.DependencyManager.Get<ISerializer>();

            Node node = nodeRegistry.GetOrCreate(ip: configuration.GetValue<string>("worker"));
            var speaker = new BoldSpeaker(node, serializer);
            speaker.Connect(30).Wait();
            speaker.Start();

            _jobResultHandler.ResponsesArrived += () => { speaker.Stop(); Stop(); };
            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(t => _messageHandler.SubmitJob(speaker, new TimeoutJob(5)));
            Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(t => _jobResultHandler.RequestAllStoredJobs(speaker));
            //Task.Delay(TimeSpan.FromMinutes(2)).ContinueWith(t => Stop());
        }
    }
}