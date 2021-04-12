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
        private ITimeStamper _timeStamper;
        private TranslationTable _translationTable;
        private JobManager _jobStorage;
        private ILogger _logger;
        
        private Dictionary<Node, int> _unconfirmedRequestIds;
        private Dictionary<Node, Message> _lastMessageSent;

        public JobMessageHandler(JobManager jobStorage, TranslationTable translationTable) : 
        this(DependencyManager.Get<IGroupViewManager>(),
            DependencyManager.Get<ICommunicationManager>(),
            DependencyManager.Get<ITimeStamper>(),
            translationTable, jobStorage,
            DependencyManager.Get<ILogger>()) {}
        public JobMessageHandler(IGroupViewManager groupManager,
            ICommunicationManager communicationManager,
            ITimeStamper timeStamper,
            TranslationTable translationTable, 
            JobManager jobStorage,
            ILogger logger)
        {
            _logger = logger;
            _translationTable = translationTable;
            _timeStamper = timeStamper;
            _jobStorage = jobStorage;
            _groupManager = groupManager;
            _communicationManager = communicationManager;
            _unconfirmedRequestIds = new Dictionary<Node, int>();
            _lastMessageSent = new Dictionary<Node, Message>();
        }

        public void Init()
        {
            var groupPublisher = _groupManager.Topics.GetPublisher<JobGroupPublisher>();
            groupPublisher.RegisterForMessage(typeof(InsertionRequest), OnMessageReceived);
            groupPublisher.RegisterForMessage(typeof(InsertionResponse), OnMessageReceived);

            var clientPublisher = _communicationManager.Topics.GetPublisher<JobClientPublisher>();
            clientPublisher.RegisterForMessage(typeof(ExecutionRequest), OnMessageReceived);
            clientPublisher.RegisterForMessage(typeof(ExecutionAck), OnMessageReceived);
            clientPublisher.RegisterForMessage(typeof(ResultRequest), OnMessageReceived);
        }

        private bool IsMessageCorrect(Node node, Message received)
        {
            if (_lastMessageSent.ContainsKey(node))
            {
                Message lastSent = _lastMessageSent[node];
                _lastMessageSent.Remove(node);
                return received.IsTheExpectedMessage(lastSent);
            }

            _logger.Warning(Tag.JobManager, $"No message correctness checked because no previous message sent to node {node}");
            return true;
        }

        private void OnMessageReceived(Node node, Message message)
        {
            if (IsMessageCorrect(node, message))
            {
                if (message is ExecutionRequest executionRequest) OnExecutionRequestArrived(node, executionRequest);
                if (message is ExecutionAck executionAck) OnExecutionAckArrived(node, executionAck);
                if (message is InsertionRequest insertionRequest) OnInsertionRequestArrived(node, insertionRequest);
                if (message is InsertionResponse insertionResponse) OnInsertionResponseArrived(node, insertionResponse);
                if (message is DistributedStorageUpdate distributedStorageUpdate) OnDistributedStorageUpdateArrived(node, distributedStorageUpdate);
                if (message is ResultRequest resultRequest) OnResultRequestArrived(node, resultRequest);
            }
            else
                _logger.Warning(Tag.JobManager, $"Received message {message} is not correct so it is discarded");
        }        

        private void Send(ICommunicationManager communicationManager, Node node, Message message)
        {
            _lastMessageSent.Add(node, message);
            if(communicationManager is NetworkManager)
                message.ApplyStamp(_timeStamper);
            communicationManager.Send(node, message).Wait();
        }
        
        private void SendMulticast(Message message)
        {
            _groupManager.SendMulticast(message).Wait();
        }

        private void OnExecutionRequestArrived(Node node, ExecutionRequest message)
        {
            _logger.Log(Tag.JobManager, $"Request for an execution arrived from client {node}");
            int requestID = _translationTable.Add(message.Job);
            _unconfirmedRequestIds.Add(node, requestID);
            Send(_communicationManager, node, new ExecutionResponse(message, requestID));
            _logger.Log(Tag.JobManager, $"Response sent with a proposal request id ({requestID})");
        }

        private void OnExecutionAckArrived(Node node, ExecutionAck message)
        {
            _logger.Log(Tag.JobManager, $"Ack for an execution arrived from client {node} with request id {message.RequestID}");

            // Confirm request id
            int requestID = message.RequestID;
            if (_unconfirmedRequestIds.ContainsKey(node) && _unconfirmedRequestIds[node] == requestID)
            {
                _unconfirmedRequestIds.Remove(node);
                _translationTable.SetConfirmed(requestID);
                _logger.Log(Tag.JobManager, $"Request id confirmed");

                // Request to coordinator for an insertion
                Job job = _translationTable.Get(requestID);
                Send(_groupManager, _groupManager.View.Coordinator, new InsertionRequest(job, requestID));
                _logger.Log(Tag.JobManager, $"Insertion request to coordinator for job with request id {requestID}");
            }
            else
                _logger.Warning(Tag.JobManager, $"Client request id {requestID} does not match any request id stored");
        }

        private void OnInsertionRequestArrived(Node node, InsertionRequest message)
        {
            _logger.Log(Tag.JobManager, $"Insertion request arrived from {node}");
            _jobStorage.InsertAndAssign(message.Job);
            _logger.Log(Tag.JobManager, $"Job added to storage and assigned");
            SendMulticast(new DistributedStorageUpdate(message.Job));
            _logger.Log(Tag.JobManager, $"Multicast sent for storage sync");
            Send(_groupManager, node, new InsertionResponse(message, message.Job.ID.Value, message.RequestID));
            _logger.Log(Tag.JobManager, $"Sent back to {node} the assigned id for the inserted job");
        }

        private void OnInsertionResponseArrived(Node node, InsertionResponse message)
        {
            _logger.Log(Tag.JobManager, $"Received from the coordinator the job id assigned");
            _translationTable.SetJobID(message.RequestID, message.JobID);
            _logger.Log(Tag.JobManager, $"Translation added for request {message.RequestID} and job {message.JobID}");
        }

        private void OnDistributedStorageUpdateArrived(Node node, DistributedStorageUpdate message)
        {
            _logger.Log(Tag.JobManager, $"Storage sync message arrived");
            if (message.Job.ID.HasValue && message.Job.Node.HasValue)
            {
                _jobStorage.InsertOrUpdateExternalJob(message.Job);
                _logger.Log(Tag.JobManager, $"Added/Updated job {message.Job.ID.Value}");
            }
            else
                _logger.Warning(Tag.JobManager, $"Arrived job is malformed (id or owner null)");
        }

        private void OnResultRequestArrived(Node node, ResultRequest message)
        {
            _logger.Log(Tag.JobManager, $"Job result request arrived");
            Job fakeJob = _translationTable.Get(message.RequestID);
            if (fakeJob == null) 
            {
                _logger.Warning(Tag.JobManager, $"Job requested (id: {message.RequestID}) doesn't exists");
                return;
            }

            Job job = _jobStorage.GetJobByID(fakeJob.ID);
            if (job == null) 
            {
                _logger.Warning(Tag.JobManager, $"No job with {fakeJob.ID} is present");
                return;
            }

            _communicationManager.Send(node, new ResultResponse(job, fakeJob.ID.Value));
            
            if (job.Status == JobStatus.COMPLETED)
                _jobStorage.SetJobDeliveredToClient(job); // TODO: Serve una conferma di ricezione dal client? (ack)
        }
    }
}