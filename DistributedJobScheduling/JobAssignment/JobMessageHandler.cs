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
        private Dictionary<(Node, int), Job> _unconfirmedRequestIds;
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
            _unconfirmedRequestIds = new Dictionary<(Node, int), Job>();
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
            _unconfirmedRequestIds.Add((node, requestID), message.Job);
        }

        private void OnExecutionAckArrived(Node node, Message received)
        {
            var message = (ExecutionAck)received;
            _logger.Log(Tag.ClientCommunication, $"Ack for an execution arrived from client {node} with request id {message.RequestID}");

            // Confirm request id
            int requestID = message.RequestID;
            if (_unconfirmedRequestIds.ContainsKey((node, requestID)))
            {
                Job job = _unconfirmedRequestIds[(node, requestID)];
                _unconfirmedRequestIds.Remove((node, requestID));
                _logger.Log(Tag.ClientCommunication, $"Request id confirmed, waiting for coordinator assignment");

                // Request to coordinator for an insertion
                if (_groupManager.View.ImCoordinator)
                {
                    _jobStorage.InsertAndAssign(job);
                    _translationTable.Update(requestID, job.ID.Value);
                    _logger.Log(Tag.ClientCommunication, $"Job stored and added to the translation table");
                }
                else
                {
                    var requestMessage = new InsertionRequest(job, requestID);
                    _oldMessageHandler.SendOrKeep(_groupManager.View.Coordinator, requestMessage, () =>
                    {
                        _logger.Log(Tag.ClientCommunication, $"Insertion requested to coordinator for job with request id {requestID}");
                    });
                }
            }
            else
                _logger.Warning(Tag.ClientCommunication, $"Client request id {requestID} does not match any request id stored");
        }

        private void OnInsertionRequestArrived(Node node, Message received)
        {
            var message = (InsertionRequest)received;
            _logger.Log(Tag.ClientCommunication, $"Insertion request arrived from {node}");
            _jobStorage.InsertAndAssign(message.Job);
            _logger.Log(Tag.ClientCommunication, $"Job added to storage and assigned");
            var responseMessage = new InsertionResponse(message, message.Job.ID.Value, message.RequestID);
            _oldMessageHandler.SendOrKeep(node, responseMessage, () => 
            {
                _logger.Log(Tag.ClientCommunication, $"Sent back to {node} the assigned id for the inserted job");
            });
        }

        private void OnInsertionResponseArrived(Node node, Message received)
        {
            var message = (InsertionResponse)received;
            _translationTable.Update(message.RequestID, message.JobID);
            _logger.Log(Tag.ClientCommunication, $"Translation added for request {message.RequestID} and job {message.JobID}");
        }

        private void OnResultRequestArrived(Node node, Message received)
        {
            var message = (ResultRequest)received;
            _logger.Log(Tag.ClientCommunication, $"Job result request arrived");
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
            {
                job.Status = JobStatus.REMOVED;
                _jobStorage.UpdateJob(job);
            }
        }
    }
}