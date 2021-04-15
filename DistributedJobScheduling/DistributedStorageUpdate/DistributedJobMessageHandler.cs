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
        private List<Message> _notDeliveredRequests;

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
            _notDeliveredRequests = new List<Message>();
        }

        public void Init()
        {
            var groupPublisher = _groupManager.Topics.GetPublisher<DistributedJobUpdatePublisher>();
            groupPublisher.RegisterForMessage(typeof(DistributedStorageUpdate), OnDistributedStorageUpdateArrived);
            groupPublisher.RegisterForMessage(typeof(DistributedStorageUpdateRequest), OnDistributedStorageUpdateRequestArrived);
            _jobStorage.JobUpdated += SendDistributedStorageUpdateRequest;
            _groupManager.View.ViewChanged += OnViewChanged;
        }

        private void OnViewChanged()
        {
            // Send old messages when a stable view occured
            if (_groupManager.View.Coordinator.ID.HasValue)
            {
                _logger.Log(Tag.DistributedUpdate, $"Start to send messages ({_notDeliveredRequests.Count})");
                SendOldMessages();
                _logger.Log(Tag.DistributedUpdate, $"Sent {_notDeliveredRequests.Count} old messages");
            }
        }
        
        private bool SendOldMessages()
        {
            bool success = true;
            _notDeliveredRequests.ForEach(message => 
            {
                try 
                { 
                    if (_groupManager.View.ImCoordinator)
                        _groupManager.SendMulticast(message).Wait();
                    else
                        _groupManager.Send(_groupManager.View.Coordinator, message).Wait(); 
                    _notDeliveredRequests.Remove(message);
                }
                catch (NotDeliveredException) { success = false; }
                catch (MulticastNotDeliveredException) { success = false; }
            });
            return success;
        }

        private void SendDistributedStorageUpdateRequest(Job job)
        {
            if (_groupManager.View.ImCoordinator)
            {
                var message = new DistributedStorageUpdate(job);
                try 
                { 
                    _groupManager.SendMulticast(message).Wait();
                    _logger.Log(Tag.DistributedUpdate, $"Distributed update sent in multicast");
                }
                catch (NotDeliveredException) 
                { 
                    _notDeliveredRequests.Add(message); 
                }
            }
            else
            {
                var message = new DistributedStorageUpdateRequest(job);
                try 
                { 
                    _groupManager.Send(_groupManager.View.Coordinator, message).Wait();
                    _logger.Log(Tag.DistributedUpdate, $"Sent to coordinator a distributed update request");
                }
                catch (NotDeliveredException) 
                { 
                    _notDeliveredRequests.Add(message); 
                }
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
                    try
                    { 
                        _groupManager.SendMulticast(new DistributedStorageUpdate(message.Job)).Wait(); 
                        _logger.Log(Tag.DistributedUpdate, $"Distributed update sent in multicast");
                    }
                    catch (NotDeliveredException) 
                    { 
                        _notDeliveredRequests.Add(message);
                        return; 
                    }

                    _jobStorage.InsertOrUpdateJobLocally(message.Job);
                    _logger.Log(Tag.DistributedUpdate, $"Updated local storage");
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