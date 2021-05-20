using System;
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

namespace DistributedJobScheduling.JobAssignment
{
    public class JobMessageHandler : IInitializable
    {
        private IGroupViewManager _groupManager;
        private ICommunicationManager _communicationManager;
        private ITranslationTable _translationTable;
        private IJobStorage _jobStorage;
        private ILogger _logger;
        private Dictionary<(Node, int), IJobWork> _unconfirmedRequestIds;
        private OldMessageHandler _oldMessageHandler;


        public JobMessageHandler() : 
        this(DependencyManager.Get<IGroupViewManager>(),
            DependencyManager.Get<ICommunicationManager>(),
            DependencyManager.Get<ITranslationTable>(),
            DependencyManager.Get<IJobStorage>(),
            DependencyManager.Get<ILogger>()) {}
        public JobMessageHandler(IGroupViewManager groupManager,
            ICommunicationManager communicationManager,
            ITranslationTable translationTable, 
            IJobStorage jobStorage,
            ILogger logger)
        {
            _logger = logger;
            _translationTable = translationTable;
            _jobStorage = jobStorage;
            _groupManager = groupManager;
            _communicationManager = communicationManager;
            _unconfirmedRequestIds = new Dictionary<(Node, int), IJobWork>();
            _oldMessageHandler = new OldMessageHandler();
        }

        public void Init()
        {
            _oldMessageHandler.Init();

            var groupPublisher = _groupManager.Topics.GetPublisher<JobGroupPublisher>();
            groupPublisher.RegisterForMessage(typeof(InsertionRequest), OnInsertionRequestArrived);
            groupPublisher.RegisterForMessage(typeof(InsertionResponse), OnInsertionResponseArrived);

            var clientPublisher = _communicationManager.Topics.GetPublisher<JobClientPublisher>();
            clientPublisher.RegisterForMessage(typeof(ExecutionRequest), OnExecutionRequestArrived);
            clientPublisher.RegisterForMessage(typeof(ExecutionAck), OnExecutionAckArrived);
            clientPublisher.RegisterForMessage(typeof(ResultRequest), OnResultRequestArrived);
        }

        // From Client to Worker
        private void OnExecutionRequestArrived(Node node, Message received)
        {
            var message = (ExecutionRequest)received;
            _logger.Log(Tag.ClientCommunication, $"Request for an execution arrived from client {node}");
            int requestID = _translationTable.CreateNewIndex;
            
            try
            {
                _communicationManager.Send(node, new ExecutionResponse(message, requestID));
                _logger.Log(Tag.ClientCommunication, $"Response sent with a proposal request id ({requestID})");
            }
            catch (Exception e)
            {
                _logger.Error(Tag.ClientCommunication, $"Message {message.TimeStamp.Value} was not sent, request discarded", e);
                return;
            }

            _translationTable.StoreIndex(requestID); 
            _unconfirmedRequestIds.Add((node, requestID), message.JobWork);
        }

        // From Client to Worker, and send to Coordinator
        private void OnExecutionAckArrived(Node node, Message received)
        {
            var message = (ExecutionAck)received;
            _logger.Log(Tag.ClientCommunication, $"Ack for an execution arrived from client {node} with request id {message.RequestID}");

            // Confirm request id
            int requestID = message.RequestID;
            if (_unconfirmedRequestIds.ContainsKey((node, requestID)))
            {
                IJobWork jobWork = _unconfirmedRequestIds[(node, requestID)];
                _unconfirmedRequestIds.Remove((node, requestID));
                _logger.Log(Tag.ClientCommunication, $"Request id confirmed, waiting for coordinator assignment");

                // Request to coordinator for an insertion
                if (_groupManager.View.ImCoordinator)
                {
                    int createdJobId = _jobStorage.CreateJob(jobWork);
                    _translationTable.Update(requestID, createdJobId);
                    _logger.Log(Tag.ClientCommunication, $"Job stored and added to the translation table, RequestID:{requestID}, JobID:{createdJobId}");
                }
                else
                {
                    var requestMessage = new InsertionRequest(jobWork, requestID);
                    _oldMessageHandler.SendOrKeepToCoordinator(requestMessage, () =>
                    {
                        _logger.Log(Tag.ClientCommunication, $"Insertion requested to coordinator for job with request id {requestID}");
                    });
                }
            }
            else
                _logger.Warning(Tag.ClientCommunication, $"Client request id {requestID} does not match any request id stored");
        }

        // From Worker to Coordinator
        private void OnInsertionRequestArrived(Node node, Message received)
        {
            var message = (InsertionRequest)received;
            _logger.Log(Tag.ClientCommunication, $"Insertion request arrived from {node}");
            int createdJobId = _jobStorage.CreateJob(message.JobWork);
            _logger.Log(Tag.ClientCommunication, $"Job added to storage and assigned");
            var responseMessage = new InsertionResponse(message, createdJobId, message.RequestID);
            _oldMessageHandler.SendOrKeep(node, responseMessage, () => 
            {
                _logger.Log(Tag.ClientCommunication, $"Sent back to {node} the assigned id for the inserted job");
            });
        }

        // From Coordinator to Worker
        private void OnInsertionResponseArrived(Node node, Message received)
        {
            var message = (InsertionResponse)received;
            _translationTable.Update(message.RequestID, message.JobID);
            _logger.Log(Tag.ClientCommunication, $"Job stored and added to the translation table, RequestID:{message.RequestID}, JobID:{message.JobID}");
        }

        // From Client to Worker
        private void OnResultRequestArrived(Node node, Message received)
        {
            var message = (ResultRequest)received;
            _logger.Log(Tag.ClientCommunication, $"Job result request arrived with id {message.RequestID}");
            int? jobID = _translationTable.Get(message.RequestID);
            if (!jobID.HasValue) 
            {
                _logger.Warning(Tag.ClientCommunication, $"Job requested (id: {message.RequestID}) doesn't exists");
                return;
            }

            Job job = _jobStorage.Get(jobID.Value);
            if (job == null) 
            {
                _logger.Warning(Tag.ClientCommunication, $"No job with {jobID} is present");
                return;
            }

            var responseMessage = new ResultResponse(message, job, message.RequestID);
            try
            {
                _communicationManager.Send(node, responseMessage);
                _logger.Log(Tag.ClientCommunication, $"Result sent back to the client");
            }
            catch (Exception e)
            {
                _logger.Error(Tag.ClientCommunication, $"Message {message.TimeStamp.Value} was not sent, request discarded", e);
                return;
            }

            if (job.Status == JobStatus.COMPLETED)
                _jobStorage.UpdateStatus(job.ID, JobStatus.REMOVED);
        }
    }
}