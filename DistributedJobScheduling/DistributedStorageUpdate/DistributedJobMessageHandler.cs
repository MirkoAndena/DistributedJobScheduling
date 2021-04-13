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
        private IJobStorage _jobStorage;
        private ILogger _logger;

        public DistributedJobMessageHandler() : 
        this(DependencyManager.Get<IGroupViewManager>(),
            DependencyManager.Get<IJobStorage>(),
            DependencyManager.Get<ILogger>()) {}

        public DistributedJobMessageHandler(
            IGroupViewManager groupManager,
            IJobStorage jobStorage,
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
            if (_groupManager.View.ImCoordinator)
            {
                _logger.Log(Tag.DistributedUpdate, $"Distributed update sent in multicast");
                var message = new DistributedStorageUpdate(job);
                _groupManager.SendMulticast(message).Wait();
            }
            else
            {
                _logger.Log(Tag.DistributedUpdate, $"Sent to coordinator a distributed update request");
                var message = new DistributedStorageUpdateRequest(job);
                _groupManager.Send(_groupManager.View.Coordinator, message).Wait();
            }
        }

        // Received by coordinator: update and multicast to others
        private void OnDistributedStorageUpdateRequestArrived(Node node, Message receivedMessage)
        {
            if(_groupManager.View.ImCoordinator)
            {
                DistributedStorageUpdateRequest message = (DistributedStorageUpdateRequest)receivedMessage;
                _logger.Log(Tag.DistributedUpdate, $"Distributed update request arrived");
                if (message.Job.ID.HasValue && message.Job.Node.HasValue)
                {
                    _logger.Log(Tag.DistributedUpdate, $"Sending distributed update in multicast");
                    _groupManager.SendMulticast(new DistributedStorageUpdate(message.Job)).Wait();
                    _jobStorage.InsertOrUpdateJobLocally(message.Job);
                    _logger.Log(Tag.DistributedUpdate, $"Distributed update sent in multicast");
                }
                else
                    _logger.Warning(Tag.DistributedUpdate, $"Arrived job is malformed (id or owner null)");
            }
            else
                _logger.Error(Tag.DistributedUpdate, $"Received distributed update request from {node} while I'm not the coordinator");
        }

        private void OnDistributedStorageUpdateArrived(Node node, Message receivedMessage)
        {
            DistributedStorageUpdate message = (DistributedStorageUpdate)receivedMessage;
            _logger.Log(Tag.DistributedUpdate, $"Distributed update arrived with content: {message.Job.ToString()}");
            if (message.Job.ID.HasValue && message.Job.Node.HasValue)
                _jobStorage.InsertOrUpdateJobLocally(message.Job);
            else
                _logger.Warning(Tag.DistributedUpdate, $"Arrived job is malformed (id or owner null)");
        }
    }
}