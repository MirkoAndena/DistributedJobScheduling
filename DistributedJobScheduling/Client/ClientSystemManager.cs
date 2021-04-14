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
        private ClientStore _store;

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
            
            _store = new ClientStore();
            RegisterSubSystem<ClientStore>(_store);

            _messageHandler = new JobInsertionMessageHandler(_store);
            RegisterSubSystem<JobInsertionMessageHandler>(_messageHandler);
            _jobResultHandler = new JobResultMessageHandler(_store);
            RegisterSubSystem<JobResultMessageHandler>(_jobResultHandler);
        }

        protected override void OnSystemStarted() => Main();

        private async Task Main()
        {
            var nodeRegistry = DependencyInjection.DependencyManager.Get<INodeRegistry>();
            var configuration = DependencyInjection.DependencyManager.Get<IConfigurationService>();
            var serializer = DependencyInjection.DependencyManager.Get<ISerializer>();

            Node node = nodeRegistry.GetOrCreate(ip: configuration.GetValue<string>("worker"));
            var speaker = new BoldSpeaker(node, serializer);
            speaker.Connect(30).Wait();
            speaker.Start();

            int batches = 4;
            int totalWidth = 256;
            int totalHeight = 256;
            int iterations = 1000;

            int batchesPerSide = batches / 2;
            for(int i = 0; i < batchesPerSide; i++)
            {
                int horizontalBatchSize = totalWidth / batchesPerSide;
                int startingX = i * horizontalBatchSize;
                for(int j = 0; j < batchesPerSide; j++)
                {
                    int verticalBatchSize = totalHeight / batchesPerSide;
                    int startingY = j * verticalBatchSize;
                    _messageHandler.SubmitJob(speaker, new MandlebrotJob(new Rectangle(startingX, startingY, horizontalBatchSize, verticalBatchSize), totalWidth, totalHeight, iterations));
                }
            }

            bool hasFinised = false;
            ILogger logger = DependencyManager.Get<ILogger>();
            _jobResultHandler.ResponsesArrived += () => 
            {
                int nonFinished = _store.ClientJobs(result => result == null).Count;
                if(nonFinished > 0)
                {
                    Console.WriteLine(nonFinished + " job not finished yet");
                    return;
                }

                logger.Flush();
                    
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
    }
}