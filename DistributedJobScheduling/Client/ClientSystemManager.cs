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
using DistributedJobScheduling.Client.Work;
using DistributedJobScheduling.Utils;
using System.Threading;

namespace DistributedJobScheduling.Client
{
    using Storage = Dictionary<int, ClientJob>;
    public class ClientSystemManager : SystemLifeCycle
    {
        #region Paths

        private const string ROOT = "./AppDataClient/";
        private string STORAGE_PATH = $"{ROOT}/storage.json";

        #endregion

        public ClientSystemManager()
        {
            RegisterSubSystem<IConfigurationService, DictConfigService>(new DictConfigService());
        }

        // args[0] => client
        // args[1] => worker IP
        // args[2..] => work type and params
        protected override void CreateConfiguration(IConfigurationService configurationService, string[] args)
        {        
            // Worker IP
            string ip = ArgsUtils.GetStringParam(args, 1, "WORKER");
            bool correctIp = ip != null && NetworkUtils.IsAnIp(ip);
            if (!correctIp) throw new Exception("worker (remote) ip not valid");

            // Pseudo-Unique ID
            var now = DateTime.Now;
            var id = now.Millisecond + now.Second << 4 + now.Minute << 8; // funzione a caso per generare un numero pseudo-univoco

            // Work
            IWork work = GetWorkFromArgs(args);

            configurationService.SetValue<int>("id", id);
            configurationService.SetValue<string>("worker", ip);
            configurationService.SetValue<IWork>("work", work);
            configurationService.SetValue<int>("batch_size", 10);
        }

        private IWork GetWorkFromArgs(string[] args)
        {
            if (ArgsUtils.IsPresent(args, "dummy"))
            {
                int timeout = ArgsUtils.GetIntParam(args, 3, "TIMEOUT");
                int count = ArgsUtils.GetIntParam(args, 4, "COUNT");
                return new DummyWork(timeout, count);
            }
            if (ArgsUtils.IsPresent(args, "rdummy"))
            {
                int count = ArgsUtils.GetIntParam(args, 3, "COUNT");
                return new DummyWork(count);
            }
            if (ArgsUtils.IsPresent(args, "dividers"))
            {
                int startNumber = ArgsUtils.GetIntParam(args, 3, "START");
                int endNumber = ArgsUtils.GetIntParam(args, 4, "END");
                return new DividersWork(startNumber, endNumber);
            }
            throw new Exception("No work specified");
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

        protected override void OnSystemStarted()
        {
            var configuration = DependencyInjection.DependencyManager.Get<IConfigurationService>();
            IWork work = configuration.GetValue<IWork>("work"); 
            Main(work);
        }

        private async void Main(IWork work)
        {
            var logger = DependencyManager.Get<ILogger>();
            var speaker = CreateConnection();
            if (speaker == null)
                logger.Fatal(Tag.WorkerCommunication, "Can't communicate with network", new Exception($"Speaker can't connect to worker"));

            var store = DependencyInjection.DependencyManager.Get<IClientStore>();

            var messageHandler = DependencyInjection.DependencyManager.Get<IJobInsertionMessageHandler>();

            var configuration = DependencyInjection.DependencyManager.Get<IConfigurationService>();
            int batch_size = configuration.GetValue<int>("batch_size", 1);

            List<IJobWork> jobs = work.CreateJobs();
            logger.Log(Tag.ClientMain, $"Jobs to execute: {jobs.Count}");
            List<int> jobIds = new List<int>();

            for (int i = 0; i < jobs.Count / batch_size; i++)
            {
                logger.Log(Tag.ClientMain, $"Start batch from {i * batch_size} to {(i + 1) * batch_size}");
                List<IJobWork> batch = jobs.GetRange(i * batch_size, batch_size);
                jobIds.AddRange(await ExecuteBatch(speaker, batch, work));
                logger.Log(Tag.ClientMain, "Batch finished");
            }
            
            logger.Log(Tag.ClientMain, "All jabs were be executed, calculating result...");

            // Creating final result
            List<IJobResult> results = store.Results(id => jobIds.Contains(id));
            work.ComputeResult(results, ROOT);

            logger.Log(Tag.ClientMain, "Result calculated, system in shutdown");

            speaker.Stop(); 
            SystemShutdown.Invoke(); 
        }

        private async Task<List<int>> ExecuteBatch(BoldSpeaker speaker, List<IJobWork> jobs, IWork work)
        {
            var messageHandler = DependencyInjection.DependencyManager.Get<IJobInsertionMessageHandler>();
            var jobResultHandler = DependencyInjection.DependencyManager.Get<IJobResultMessageHandler>();
            var store = DependencyInjection.DependencyManager.Get<IClientStore>();
            var logger = DependencyManager.Get<ILogger>();

            var semaphore = new SemaphoreSlim(0);

            jobResultHandler.ResponsesArrived += async notCompleted => 
            {
                if (notCompleted.Count == 0)        
                {
                    semaphore.Release();
                }
                else
                {
                    logger.Log(Tag.ClientMain, $"{notCompleted.Count} jobs were not completed, retrying after 10 seconds");
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    logger.Log(Tag.ClientMain, $"Requesting {notCompleted.Count} not completed jobs");
                    jobResultHandler.RequestJobs(speaker, notCompleted);
                }
            };

            List<int> requests = null;
            messageHandler.JobsSubmitted += submittedRequests => requests = submittedRequests;
            messageHandler.SubmitJob(speaker, jobs);

            await Task.Delay(TimeSpan.FromSeconds(10));
            jobResultHandler.RequestAllStoredJobs(speaker);

            await semaphore.WaitAsync();
            return requests;
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

        protected override ILogger GetLogger() => DependencyInjection.DependencyManager.Get<ILogger>();
    }
}