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
    public class JobInsertionMessageHandler : IStartable
    {
        private BoldSpeaker _speaker;
        private ILogger _logger;
        private ISerializer _serializer;
        private ITimeStamper _timeStamper;
        private ClientStore _store;
        private Message _previousMessage;
        private INodeRegistry _nodeRegistry;
        private IConfigurationService _configuration;

        public JobInsertionMessageHandler(ClientStore store) : this (
            store,
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<ISerializer>(),
            DependencyInjection.DependencyManager.Get<ITimeStamper>(),
            DependencyInjection.DependencyManager.Get<INodeRegistry>(),
            DependencyInjection.DependencyManager.Get<IConfigurationService>()) { }

        public JobInsertionMessageHandler(ClientStore store, ILogger logger, ISerializer serializer, ITimeStamper timeStamper, INodeRegistry nodeRegistry, IConfigurationService configuration)
        {
            _store = store;
            _logger = logger;
            _serializer = serializer;
            _timeStamper = timeStamper;
            _nodeRegistry = nodeRegistry;
            _configuration = configuration;
            var now = DateTime.Now;
        }


        public void SubmitJob(Job job)
        {
            Node node = _nodeRegistry.GetOrCreate(ip: _configuration.GetValue<string>("worker"));
            _speaker = new BoldSpeaker(node, _serializer);
            _speaker.Connect(30).Wait();
            _speaker.Start();
            _speaker.MessageReceived += OnMessageReceived;

            Message message = new ExecutionRequest(job);
            _previousMessage = message;
            _speaker.Send(message.ApplyStamp(_timeStamper)).Wait();
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
                if (message is ExecutionResponse response)
                {
                    var job = new ClientJob(response.RequestID);
                    _store.StoreClientJob(job);
                    _logger.Log(Tag.ClientJobMessaging, $"Stored request id ({job.ID})");
                    
                    Message ack = new ExecutionAck(response, job.ID);
                    _speaker.Send(ack.ApplyStamp(_timeStamper)).Wait();
                    _logger.Log(Tag.ClientJobMessaging, $"Job successfully assigned to network, RequestID: {job.ID}");
                }
            }
            else
                _logger.Warning(Tag.ClientJobMessaging, "Received message was rejected");
        }

        public void Start()
        {
            // Nothing
        }
    }
}