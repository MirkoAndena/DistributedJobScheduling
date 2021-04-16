using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using System;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Basic.Speakers;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;
using static DistributedJobScheduling.Communication.Basic.Node;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.Communication.Messaging;

namespace DistributedJobScheduling.Client
{
    public interface IJobResultMessageHandler
    {
        event Action ResponsesArrived;
        void RequestAllStoredJobs(BoldSpeaker speaker);
        void RequestJob(BoldSpeaker speaker, ClientJob job);
    }

    public class JobResultMessageHandler : IStartable, IJobResultMessageHandler
    {
        private BoldSpeaker _speaker;
        private ILogger _logger;
        private ISerializer _serializer;
        private IClientStore _store;
        private ITimeStamper _timeStamper;
        private INodeRegistry _nodeRegistry;
        private IConfigurationService _configuration;
        private int _pendingRequests;
        public event Action ResponsesArrived;
        private bool _registered;

        public JobResultMessageHandler() : this (
            DependencyInjection.DependencyManager.Get<IClientStore>(),
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<ISerializer>(),
            DependencyInjection.DependencyManager.Get<ITimeStamper>(),
            DependencyInjection.DependencyManager.Get<INodeRegistry>(),
            DependencyInjection.DependencyManager.Get<IConfigurationService>()) { }

        public JobResultMessageHandler(IClientStore store, ILogger logger, ISerializer serializer, ITimeStamper timeStamper, INodeRegistry nodeRegistry, IConfigurationService configuration)
        {
            _store = store;
            _logger = logger;
            _serializer = serializer;
            _timeStamper = timeStamper;
            _nodeRegistry = nodeRegistry;
            _configuration = configuration;
            _pendingRequests = 0;
            _registered = false;
        }

        public void RequestAllStoredJobs(BoldSpeaker speaker)
        {
            _store.ClientJobs(result => result == null).ForEach(job => 
            {
                RequestJob(speaker, job);
                _pendingRequests++;
            });
        }

        public void RequestJob(BoldSpeaker speaker, ClientJob job)
        {
            _logger.Log(Tag.WorkerCommunication, $"Requesting result for {job.ID}");
            _speaker = speaker;
            if (!_registered)
            {
                _speaker.MessageReceived += OnMessageReceived;
                _registered = true;
            }

            Message message = new ResultRequest(job.ID);

            try
            {
                _speaker.Send(message.ApplyStamp(_timeStamper)).Wait();
            }
            catch (Exception e)
            {
                _logger.Error(Tag.WorkerCommunication, "Job request not sent", e);
            }
        }

        public void Start()
        {
            // Nothing
        }

        public void Stop()
        {
            if (_registered)
            {
                _speaker.MessageReceived -= OnMessageReceived;
                _registered = false;
            }
        }

        private void OnMessageReceived(Node node, Message message)
        {
            if (message is ResultResponse response)
            {
                _logger.Log(Tag.WorkerCommunication, $"Job requested {response.ClientJobId} is {response.Status}");
                if (response.Status == JobStatus.COMPLETED)
                {
                    _store.UpdateClientJobResult(response.ClientJobId, response.Result);
                    _logger.Log(Tag.WorkerCommunication, $"Job result updated into storage");
                }

                _pendingRequests--;
                _logger.Log(Tag.WorkerCommunication, $"{_pendingRequests} request remains");
                if (_pendingRequests == 0) 
                {
                    ResponsesArrived?.Invoke();
                    _logger.Log(Tag.WorkerCommunication, $"All jobs are executed and results are returned");
                }
            }
        }
    }
}