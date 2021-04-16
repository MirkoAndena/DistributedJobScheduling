using System;

using System.Collections.Generic;

using System.Drawing;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Basic.Speakers;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.LeaderElection;
using DistributedJobScheduling.LeaderElection.KeepAlive;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.VirtualSynchrony;

using static DistributedJobScheduling.Communication.Basic.Node;
using DistributedJobScheduling.DependencyInjection;

namespace DistributedJobScheduling.Client
{
    using Storage = List<ClientJob>;
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
            RegisterSubSystem<IClientStore, ClientStore>(new ClientStore());
            RegisterSubSystem<IJobInsertionMessageHandler, JobInsertionMessageHandler>(new JobInsertionMessageHandler());
            RegisterSubSystem<IJobResultMessageHandler, JobResultMessageHandler>(new JobResultMessageHandler());
        }

        protected override void OnSystemStarted() => Main();

        private async Task Main()
        {
            var logger = DependencyManager.Get<ILogger>();
            var speaker = CreateConnection();
            if (speaker == null)
                logger.Fatal(Tag.WorkerCommunication, "Can't communicate with network", new Exception($"Speaker can't connect to worker"));


            var store = DependencyInjection.DependencyManager.Get<IClientStore>();

            List<MandlebrotJob> jobs = Mandlebrot.CreateJobs(4, 256, 1000);
            _messageHandler.SubmitJob(speaker, jobs);

            bool hasFinised = false;
            _jobResultHandler.ResponsesArrived += () => 
            {
                int nonFinished = store.ClientJobs(result => result == null).Count;
                if(nonFinished > 0)
                {
                    Console.WriteLine(nonFinished + " job not finished yet");
                    return;
                }
                    
                Console.WriteLine("All responses arrived, shutdown");
                hasFinised = true;
                speaker.Stop(); 
                Shutdown.Invoke(); 
            };

            while(!hasFinised)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                _jobResultHandler.RequestAllStoredJobs(speaker);
            }
        }

        private BoldSpeaker CreateConnection()
        {
            var nodeRegistry = DependencyInjection.DependencyManager.Get<INodeRegistry>();
            var configuration = DependencyInjection.DependencyManager.Get<IConfigurationService>();
            var serializer = DependencyInjection.DependencyManager.Get<ISerializer>();
            Node node = nodeRegistry.GetOrCreate(ip: configuration.GetValue<string>("worker"));
            var speaker = new BoldSpeaker(node, serializer);

            try
            {
                speaker.Connect(30).Wait();
                speaker.Start();
                return speaker;
            }
            catch
            {
                return null;
            }
        }
    }
}