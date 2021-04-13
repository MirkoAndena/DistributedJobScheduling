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
        private IJobStorage _jobStorage;
        private ILogger _logger;
        
        private Dictionary<Node, (int, Job)> _unconfirmedRequestIds;
        private Dictionary<Node, Message> _lastMessageSent;

        public JobMessageHandler(TranslationTable translationTable) : 
        this(DependencyManager.Get<IGroupViewManager>(),
            DependencyManager.Get<ICommunicationManager>(),
            DependencyManager.Get<ITimeStamper>(),
            translationTable,
            DependencyManager.Get<IJobStorage>(),
            DependencyManager.Get<ILogger>()) {}
        public JobMessageHandler(IGroupViewManager groupManager,
            ICommunicationManager communicationManager,
            ITimeStamper timeStamper,
            TranslationTable translationTable, 
            IJobStorage jobStorage,
            ILogger logger)
        {
            _logger = logger;
            _translationTable = translationTable;
            _timeStamper = timeStamper;
            _jobStorage = jobStorage;
            _groupManager = groupManager;
            _communicationManager = communicationManager;
            _unconfirmedRequestIds = new Dictionary<Node, (int, Job)>();
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

            _logger.Warning(Tag.ClientCommunication, $"No message correctness checked because no previous message sent to node {node}");
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
                if (message is ResultRequest resultRequest) OnResultRequestArrived(node, resultRequest);
            }
            else
                _logger.Warning(Tag.ClientCommunication, $"Received message {message} is not correct so it is discarded");
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
            _logger.Log(Tag.ClientCommunication, $"Request for an execution arrived from client {node}");
            int requestID = _translationTable.CreateNewIndex;
            _unconfirmedRequestIds.Add(node, (requestID, message.Job));
            Send(_communicationManager, node, new ExecutionResponse(message, requestID));
            _logger.Log(Tag.ClientCommunication, $"Response sent with a proposal request id ({requestID})");
        }

        private void OnExecutionAckArrived(Node node, ExecutionAck message)
        {
            _logger.Log(Tag.ClientCommunication, $"Ack for an execution arrived from client {node} with request id {message.RequestID}");

            // Confirm request id
            int requestID = message.RequestID;
            if (_unconfirmedRequestIds.ContainsKey(node) && _unconfirmedRequestIds[node].Item1 == requestID)
            {
                Job job = _unconfirmedRequestIds[node].Item2;
                _unconfirmedRequestIds.Remove(node);
                _logger.Log(Tag.ClientCommunication, $"Request id confirmed, waiting for coordinator assignment");

                // Request to coordinator for an insertion
                if (_groupManager.View.ImCoordinator)
                {
                    _jobStorage.InsertAndAssign(job);
                    _translationTable.Add(requestID, job.ID.Value);
                    _logger.Log(Tag.ClientCommunication, $"Job stored and added to the translation table");
                }
                else
                {
                    Send(_groupManager, _groupManager.View.Coordinator, new InsertionRequest(job, requestID));
                    _logger.Log(Tag.ClientCommunication, $"Insertion requested to coordinator for job with request id {requestID}");
                }
            }
            else
                _logger.Warning(Tag.ClientCommunication, $"Client request id {requestID} does not match any request id stored");
        }

        private void OnInsertionRequestArrived(Node node, InsertionRequest message)
        {
            _logger.Log(Tag.ClientCommunication, $"Insertion request arrived from {node}");
            _jobStorage.InsertAndAssign(message.Job);
            _logger.Log(Tag.ClientCommunication, $"Job added to storage and assigned");
            Send(_groupManager, node, new InsertionResponse(message, message.Job.ID.Value, message.RequestID));
            _logger.Log(Tag.ClientCommunication, $"Sent back to {node} the assigned id for the inserted job");
        }

        private void OnInsertionResponseArrived(Node node, InsertionResponse message)
        {
            _translationTable.Add(message.RequestID, message.JobID);
            _logger.Log(Tag.ClientCommunication, $"Translation added for request {message.RequestID} and job {message.JobID}");
        }

        private void OnResultRequestArrived(Node node, ResultRequest message)
        {
            _logger.Log(Tag.ClientCommunication, $"Job result request arrived");
            int? jobID = _translationTable.Get(message.RequestID);
            if (jobID == null) 
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

            _communicationManager.Send(node, new ResultResponse(message, job, message.RequestID));
            
            if (job.Status == JobStatus.COMPLETED)
                _jobStorage.SetJobDeliveredToClient(job); // TODO: Serve una conferma di ricezione dal client? (ack)
        }
    }
}