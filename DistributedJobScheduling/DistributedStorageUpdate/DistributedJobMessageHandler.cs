using System.IO.Compression;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Communication;

namespace DistributedJobScheduling.DistributedJobUpdate
{
    public class DistributedJobMessageHandler : IInitializable
    {
        private IGroupViewManager _groupManager;
        private JobStorage _jobStorage;
        private ILogger _logger;

        public DistributedJobMessageHandler(JobStorage jobStorage) : 
        this(DependencyManager.Get<IGroupViewManager>(),
            jobStorage,
            DependencyManager.Get<ILogger>()) {}

        public DistributedJobMessageHandler(
            IGroupViewManager groupManager,
            JobStorage jobStorage,
            ILogger logger)
        {
            _logger = logger;
            _jobStorage = jobStorage;
            _groupManager = groupManager;
        }

        public void Init()
        {
            var groupPublisher = _groupManager.Topics.GetPublisher<DistributedJobUpdatePublisher>();
            groupPublisher.RegisterForMessage(typeof(DistributedStorageUpdate), OnDistributedStorageUpdateArrived);
            groupPublisher.RegisterForMessage(typeof(DistributedStorageUpdateRequest), OnDistributedStorageUpdateRequestArrived);
            _jobStorage.JobUpdated += SendDistributedStorageUpdateRequest;
        }
        
        private void SendDistributedStorageUpdateRequest(Job job)
        {
            var message = new DistributedStorageUpdateRequest(job);
            _groupManager.Send(_groupManager.View.Coordinator, message);
        }

        // Received by coordinator: update and multicast to others
        private void OnDistributedStorageUpdateRequestArrived(Node node, Message receivedMessage)
        {
            DistributedStorageUpdateRequest message = (DistributedStorageUpdateRequest)receivedMessage;
            _logger.Log(Tag.DistributedUpdate, $"Distributed update request arrived");
            if (message.Job.ID.HasValue && message.Job.Node.HasValue)
            {
                _jobStorage.InsertOrUpdateJobLocally(message.Job);
                _groupManager.SendMulticast(new DistributedStorageUpdate(message.Job));
                _logger.Log(Tag.DistributedUpdate, $"Distributed update sent in multicast");
            }
            else
                _logger.Warning(Tag.DistributedUpdate, $"Arrived job is malformed (id or owner null)");
        }

        private void OnDistributedStorageUpdateArrived(Node node, Message receivedMessage)
        {
            DistributedStorageUpdate message = (DistributedStorageUpdate)receivedMessage;
            _logger.Log(Tag.JobManager, $"Storage sync message arrived");
            if (message.Job.ID.HasValue && message.Job.Node.HasValue)
                _jobStorage.InsertOrUpdateJobLocally(message.Job);
            else
                _logger.Warning(Tag.JobManager, $"Arrived job is malformed (id or owner null)");
        }
    }
}