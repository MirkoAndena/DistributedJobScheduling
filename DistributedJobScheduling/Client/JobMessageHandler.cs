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
    public class JobMessageHandler
    {
        private BoldSpeaker _speaker;
        private ILogger _logger;
        private ISerializer _serializer;
        private ClientStore _store;
        private int _id;
        private Message _previousMessage;
        private INodeRegistry _nodeRegistry;
        private IConfigurationService _configuration;

        public JobMessageHandler(ClientStore store) : this (
            store,
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<ISerializer>(),
            DependencyInjection.DependencyManager.Get<INodeRegistry>(),
            DependencyInjection.DependencyManager.Get<IConfigurationService>()) { }

        public JobMessageHandler(ClientStore store, ILogger logger, ISerializer serializer, INodeRegistry nodeRegistry, IConfigurationService configuration)
        {
            _store = store;
            _logger = logger;
            _serializer = serializer;
            _nodeRegistry = nodeRegistry;
            _configuration = configuration;
            var now = DateTime.Now;
            _id = now.Millisecond + now.Second << 4 + now.Minute << 8; // funzione a caso per generare un numero pseudo-univoco
        }


        public void SubmitJob(Job job)
        {
            Node node = _nodeRegistry.GetOrCreate(ip: _configuration.GetValue<string>("worker"));
            _speaker = new BoldSpeaker(node, _serializer);
            _speaker.Connect(30).Wait();
            _speaker.Start();
            _speaker.MessageReceived += OnMessageReceived;

            Message message = new ExecutionRequest(job);

            // Initially remote node hasn't an ID but is updated in future
            message.SenderID = _id;
            if (node.ID.HasValue)
                message.ReceiverID = node.ID.Value;

            _previousMessage = message;
            _speaker.Send(message);
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
                    // Update remote node info
                    if (!node.ID.HasValue && response.SenderID.HasValue)
                    {
                        _nodeRegistry.UpdateNodeID(node, response.SenderID.Value);
                        _logger.Log(Tag.ClientJobMessaging, $"Updated remote node ID {node.ToString()}");
                    }

                    var job = new ClientJob(response.RequestID);
                    _store.StoreClientJob(job);
                    _logger.Log(Tag.ClientJobMessaging, $"Stored request id ({job.ID})");
                    
                    Message ack = new ExecutionAck(response, job.ID);
                    ack.SenderID = _id;
                    ack.ReceiverID = node.ID.Value;
                    _speaker.Send(ack);
                    this.Stop();
                }
            }
            else
                _logger.Warning(Tag.ClientJobMessaging, "Received message was rejected");
        }
    }
}