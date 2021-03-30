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

        public JobMessageHandler(ClientStore store) : this (
            store,
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<ISerializer>()) { }

        public JobMessageHandler(ClientStore store, ILogger logger, ISerializer serializer)
        {
            _store = store;
            _logger = logger;
            _serializer = serializer;
            var now = DateTime.Now;
            _id = now.Millisecond + now.Second << 4 + now.Minute << 8; // funzione a caso per generare un numero pseudo-univoco
        }


        public void SubmitJob(Node node, Job job)
        {
            _speaker = new BoldSpeaker(node, _serializer);
            _speaker.Start();
            _speaker.Connect(30).Wait();
            _speaker.MessageReceived += OnMessageReceived;

            Message message = new ExecutionRequest(job);
            message.SenderID = _id;
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
                    var job = new ClientJob(response.RequestID);
                    _store.StoreClientJob(job);
                    _logger.Log(Tag.Communication, $"Stored request id ({job.ID})");
                    
                    Message ack = new ExecutionAck(response, job.ID);
                    ack.SenderID = _id;
                    ack.ReceiverID = node.ID.Value;
                    _speaker.Send(ack);
                    this.Stop();
                }
            }
            else
                _logger.Warning(Tag.Communication, "Received message was rejected");
        }
    }
}