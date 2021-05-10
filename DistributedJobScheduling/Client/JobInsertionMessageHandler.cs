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
        event Action<List<int>> JobsSubmitted;
        void SubmitJob<T>(List<T> jobs) where T : IJobWork;
    }

    public class JobInsertionMessageHandler : IJobInsertionMessageHandler, IInitializable
    {
        private ILogger _logger;
        private ISerializer _serializer;
        private ITimeStamper _timeStamper;
        private IClientStore _store;
        private INodeRegistry _nodeRegistry;
        private IConfigurationService _configuration;
        private IClientCommunication _clientCommunication;
        private int _submissionCount;
        private List<int> _requests;
        public event Action<List<int>> JobsSubmitted;

        public JobInsertionMessageHandler() : this (
            DependencyInjection.DependencyManager.Get<IClientStore>(),
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<ISerializer>(),
            DependencyInjection.DependencyManager.Get<ITimeStamper>(),
            DependencyInjection.DependencyManager.Get<INodeRegistry>(),
            DependencyInjection.DependencyManager.Get<IConfigurationService>(),
            DependencyInjection.DependencyManager.Get<IClientCommunication>()) { }

        public JobInsertionMessageHandler(IClientStore store, ILogger logger, ISerializer serializer, ITimeStamper timeStamper, INodeRegistry nodeRegistry, IConfigurationService configuration, IClientCommunication clientCommunication)
        {
            _store = store;
            _logger = logger;
            _serializer = serializer;
            _timeStamper = timeStamper;
            _nodeRegistry = nodeRegistry;
            _configuration = configuration;
            _clientCommunication = clientCommunication;
            var now = DateTime.Now;
            _requests = new List<int>();
        }

        public void Init()
        {
            _clientCommunication.MessageReceived += OnMessageReceived;
        }

        public void SubmitJob<T>(List<T> jobs) where T : IJobWork
        {
            _submissionCount = jobs.Count;
            _requests.Clear();
            jobs.ForEach(job =>
            {
                Message message = new ExecutionRequest(job);
                _clientCommunication.Send(message);
                _logger.Log(Tag.WorkerCommunication, $"Job submit request sent");
            });
        }

        private void OnMessageReceived(Node node, Message message)
        {
            if (message is ExecutionResponse response)
            {
                var job = new ClientJob(response.RequestID);
                Message ack = new ExecutionAck(response, job.ID);
                _clientCommunication.Send(ack.ApplyStamp(_timeStamper));
                _logger.Log(Tag.WorkerCommunication, $"Job successfully assigned to network, RequestID: {job.ID}");
                
                _store.StoreClientJob(job);
                _requests.Add(job.ID);

                if (_requests.Count == _submissionCount)
                    JobsSubmitted?.Invoke(_requests);
            }
        }
    }
}