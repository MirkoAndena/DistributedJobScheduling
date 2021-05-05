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
        void AttachSpeaker(Speaker speaker);
        event Action<List<int>> JobsSubmitted;
        void SubmitJob<T>(List<T> jobs) where T : IJobWork;
    }

    public class JobInsertionMessageHandler : IJobInsertionMessageHandler
    {
        private Speaker _speaker;
        private ILogger _logger;
        private ISerializer _serializer;
        private ITimeStamper _timeStamper;
        private IClientStore _store;
        private INodeRegistry _nodeRegistry;
        private IConfigurationService _configuration;
        private int _submissionCount;
        private List<int> _requests;
        public event Action<List<int>> JobsSubmitted;

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
            _requests = new List<int>();
        }
        
        public void AttachSpeaker(Speaker speaker)
        {
            _speaker = speaker;
            _speaker.MessageReceived += OnMessageReceived;
        }

        public void SubmitJob<T>(List<T> jobs) where T : IJobWork
        {
            _submissionCount = jobs.Count;
            _requests.Clear();
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

        private void OnMessageReceived(Node node, Message message)
        {
            if (message is ExecutionResponse response)
            {
                var job = new ClientJob(response.RequestID);
                Message ack = new ExecutionAck(response, job.ID);
                _speaker.Send(ack.ApplyStamp(_timeStamper)).Wait();
                _logger.Log(Tag.WorkerCommunication, $"Job successfully assigned to network, RequestID: {job.ID}");
                
                _store.StoreClientJob(job);
                _requests.Add(job.ID);

                if (_requests.Count == _submissionCount)
                    JobsSubmitted?.Invoke(_requests);
            }
        }
    }
}