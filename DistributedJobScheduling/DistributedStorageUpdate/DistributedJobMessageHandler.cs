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
        private OldMessageHandler _oldMessageHandler;

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
            _oldMessageHandler = new OldMessageHandler();
        }

        public void Init()
        {
            var groupPublisher = _groupManager.Topics.GetPublisher<DistributedJobUpdatePublisher>();
            groupPublisher.RegisterForMessage(typeof(DistributedStorageUpdate), OnDistributedStorageUpdateArrived);
            groupPublisher.RegisterForMessage(typeof(DistributedStorageUpdateRequest), OnDistributedStorageUpdateRequestArrived);
            _jobStorage.JobUpdated += SendDistributedStorageUpdateRequest;
            _oldMessageHandler.Init();
        }

        private void SendDistributedStorageUpdateRequest(Job job)
        {
            if (_groupManager.View.CoordinatorExists)
            {
                if (_groupManager.View.ImCoordinator)
                {
                    var message = new DistributedStorageUpdate(job);
                    _oldMessageHandler.SendMulticastOrKeep(message, () =>
                    {
                        _logger.Log(Tag.DistributedUpdate, $"Distributed update sent in multicast");
                    });
                }
                else
                {
                    var message = new DistributedStorageUpdateRequest(job);
                    _oldMessageHandler.SendOrKeep(_groupManager.View.Coordinator, message, () =>
                    {
                        _logger.Log(Tag.DistributedUpdate, $"Sent to coordinator a distributed update request");
                    });
                }
            }
            else
            {
                _logger.Fatal(Tag.DistributedUpdate, "Voglio aggiornare lo storage distribuito ma non c'è il coordinator", new System.Exception("No coordinator during a send distribution request"));
            }
        }

        // Received by coordinator: update and multicast to others
        private void OnDistributedStorageUpdateRequestArrived(Node node, Message receivedMessage)
        {
            if(_groupManager.View.ImCoordinator)
            {
                DistributedStorageUpdateRequest message = (DistributedStorageUpdateRequest)receivedMessage;
                _logger.Log(Tag.DistributedUpdate, $"Distributed update request arrived,  message: {message.ToString()}");
                var updateMessage = new DistributedStorageUpdate(message.Job);
                _oldMessageHandler.SendMulticastOrKeep(updateMessage, () =>
                {
                    _logger.Log(Tag.DistributedUpdate, $"Distributed update sent in multicast");
                    _jobStorage.CommitUpdate(message.Job);
                    _logger.Log(Tag.DistributedUpdate, $"Updated local storage with job: {message.Job.ToString()}");
                });         
            }
            else
                _logger.Error(Tag.DistributedUpdate, $"Received distributed update request from {node} while I'm not the coordinator");
        }

        private void OnDistributedStorageUpdateArrived(Node node, Message receivedMessage)
        {
            DistributedStorageUpdate message = (DistributedStorageUpdate)receivedMessage;
            _logger.Log(Tag.DistributedUpdate, $"Distributed update arrived with content: {message.ToString()}");
            _jobStorage.CommitUpdate(message.Job);
        }
    }
}