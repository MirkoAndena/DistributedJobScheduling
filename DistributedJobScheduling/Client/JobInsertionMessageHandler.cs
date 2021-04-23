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
using System.Collections.Generic;

namespace DistributedJobScheduling.Client
{
    public interface IJobInsertionMessageHandler
    {
        List<int> Requests { get; } 
        void SubmitJob<T>(BoldSpeaker speaker, List<T> jobs) where T : IJobWork;
    }

    public class JobInsertionMessageHandler : IStartable, IJobInsertionMessageHandler
    {
        private BoldSpeaker _speaker;
        private ILogger _logger;
        private ISerializer _serializer;
        private ITimeStamper _timeStamper;
        private IClientStore _store;
        private INodeRegistry _nodeRegistry;
        private IConfigurationService _configuration;
        private bool _registered;
        public List<int> Requests { get; private set; } 

        public JobInsertionMessageHandler() : this (
            DependencyInjection.DependencyManager.Get<IClientStore>(),
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<ISerializer>(),
            DependencyInjection.DependencyManager.Get<ITimeStamper>(),
            DependencyInjection.DependencyManager.Get<INodeRegistry>(),
            DependencyInjection.DependencyManager.Get<IConfigurationService>()) { }

        public JobInsertionMessageHandler(IClientStore store, ILogger logger, ISerializer serializer, ITimeStamper timeStamper, INodeRegistry nodeRegistry, IConfigurationService configuration)
        {
            _store = store;
            _logger = logger;
            _serializer = serializer;
            _timeStamper = timeStamper;
            _nodeRegistry = nodeRegistry;
            _configuration = configuration;
            var now = DateTime.Now;
            _registered = false;
            Requests = new List<int>();
        }


        public void SubmitJob<T>(BoldSpeaker speaker, List<T> jobs) where T : IJobWork
        {
            _speaker = speaker;
            if (!_registered) 
            {
                _speaker.MessageReceived += OnMessageReceived;
                _registered =  true;
            }

            Requests.Clear();
            jobs.ForEach(job =>
            {
                try
                {
                    Message message = new ExecutionRequest(job);
                    _speaker.Send(message.ApplyStamp(_timeStamper)).Wait();
                    _logger.Log(Tag.WorkerCommunication, $"Job submit request sent");
                }
                catch (Exception e)
                {
                    _logger.Fatal(Tag.ClientCommunication, "An error occured during job submission", e);
                }
            });
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
            if (message is ExecutionResponse response)
            {
                var job = new ClientJob(response.RequestID);
                Message ack = new ExecutionAck(response, job.ID);
                _speaker.Send(ack.ApplyStamp(_timeStamper)).Wait();
                _logger.Log(Tag.WorkerCommunication, $"Job successfully assigned to network, RequestID: {job.ID}");
                
                _store.StoreClientJob(job);
                Requests.Add(job.ID);
                _logger.Log(Tag.WorkerCommunication, $"Stored request id ({job.ID})");
            }
        }

        public void Start()
        {
            // Nothing
        }
    }
}