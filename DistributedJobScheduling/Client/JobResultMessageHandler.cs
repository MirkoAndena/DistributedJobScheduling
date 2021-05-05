using System.Threading;
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
using System.Collections.Generic;

namespace DistributedJobScheduling.Client
{
    public interface IJobResultMessageHandler
    {
        void AttachSpeaker(Speaker speaker);
        event Action<List<int>> ResponsesArrived;
        void RequestJobs(List<int> jobs);
    }

    public class JobResultMessageHandler : IJobResultMessageHandler
    {
        private ILogger _logger;
        private ISerializer _serializer;
        private IClientStore _store;
        private ITimeStamper _timeStamper;
        private INodeRegistry _nodeRegistry;
        private IConfigurationService _configuration;

        private Speaker _speaker;
        private int _pendingRequests;
        public event Action<List<int>> ResponsesArrived;
        private List<int> _notCompleted;
        private SemaphoreSlim _semaphore;

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
            _notCompleted = new List<int>();
        }

        public void AttachSpeaker(Speaker speaker)
        {
            _speaker = speaker;
            _speaker.MessageReceived += OnMessageReceived;
        }

        public void RequestJobs(List<int> jobIds)
        {
            // Don't wait the first call
            if (_semaphore == null)
                _semaphore = new SemaphoreSlim(0, 1);
            else
                _semaphore.Wait();

            _pendingRequests = 0;
            _notCompleted.Clear();

            jobIds.ForEach(jobId => 
            {
                _logger.Log(Tag.WorkerCommunication, $"Requesting result for {jobId}");

                try
                {
                    Message message = new ResultRequest(jobId);
                    _speaker.Send(message.ApplyStamp(_timeStamper)).Wait();
                    _pendingRequests++;
                }
                catch (Exception e)
                {
                    _logger.Error(Tag.WorkerCommunication, "Job request not sent", e);
                }
            });
        }

        private void OnMessageReceived(Node node, Message message)
        {
            if (message is ResultResponse response)
            {
                _logger.Log(Tag.WorkerCommunication, $"Job requested {response.ClientJobId} is {response.Status}");
                if (response.Status == JobStatus.COMPLETED)
                {
                    _notCompleted.Remove(response.ClientJobId);
                    _store.UpdateClientJobResult(response.ClientJobId, response.Result);
                    _logger.Log(Tag.WorkerCommunication, $"Job result updated into storage");
                }
                else
                {
                    if (!_notCompleted.Contains(response.ClientJobId))
                        _notCompleted.Add(response.ClientJobId);
                }

                _pendingRequests--;
                
                if (_pendingRequests == 0) 
                {
                    _logger.Log(Tag.WorkerCommunication, $"All responses arrived");
                    ResponsesArrived?.Invoke(new List<int>(_notCompleted));
                    _semaphore.Release();
                }
            }
        }
    }
}