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

namespace DistributedJobScheduling.Client
{
    public class JobResultMessageHandler : IStartable
    {
        private BoldSpeaker _speaker;
        private ILogger _logger;
        private ISerializer _serializer;
        private ClientStore _store;
        private Message _previousMessage;
        private INodeRegistry _nodeRegistry;
        private IConfigurationService _configuration;
        private int _pendingRequests;
        public event Action ResponsesArrived;

        public JobResultMessageHandler(ClientStore store) : this (
            store,
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<ISerializer>(),
            DependencyInjection.DependencyManager.Get<INodeRegistry>(),
            DependencyInjection.DependencyManager.Get<IConfigurationService>()) { }

        public JobResultMessageHandler(ClientStore store, ILogger logger, ISerializer serializer, INodeRegistry nodeRegistry, IConfigurationService configuration)
        {
            _store = store;
            _logger = logger;
            _serializer = serializer;
            _nodeRegistry = nodeRegistry;
            _configuration = configuration;
            _pendingRequests = 0;
        }

        public void RequestAllStoredJobs()
        {
            _store.ClientJobs(result => result == null).ForEach(job => 
            {
                RequestJob(job);
                _pendingRequests++;
            });
        }

        public void RequestJob(ClientJob job)
        {
            Node node = _nodeRegistry.GetOrCreate(ip: _configuration.GetValue<string>("worker"));
            _speaker = new BoldSpeaker(node, _serializer);
            _speaker.Connect(30).Wait();
            _speaker.Start();
            _speaker.MessageReceived += OnMessageReceived;

            Message message = new ResultRequest(job.ID);
            _previousMessage = message;
            _speaker.Send(message);
        }

        public void Start()
        {
            // Nothing
        }

        public void Stop()
        {
            _speaker.MessageReceived -= OnMessageReceived;
            _speaker.Stop();
        }

        private void OnMessageReceived(Node node, Message message)
        {
            if (message.IsTheExpectedMessage(_previousMessage))
            {
                if (message is ResultResponse response)
                {
                    _logger.Log(Tag.ClientJobMessaging, $"Job requested is {response.Status}");
                    if (response.Status == JobStatus.COMPLETED)
                    {
                        _store.UpdateClientJobResult(response.ClientJobId, response.Result);
                        _logger.Log(Tag.ClientJobMessaging, $"Job result updated into storage");
                    }

                    _pendingRequests--;
                    if (_pendingRequests == 0) ResponsesArrived?.Invoke();
                }
            }
            else
                _logger.Warning(Tag.ClientJobMessaging, "Received message was rejected");
        }
    }
}