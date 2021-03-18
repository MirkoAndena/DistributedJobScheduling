using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.DistributedStorage;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.JobAssignment
{
    public class JobMessageHandler
    {
        private GroupViewManager _groupManager;
        private TranslationTable _traductionTable;
        private DistributedList _distributedList;
        private ILogger _logger;
        
        private Dictionary<Node, int> _unconfirmedRequestIds;
        private Dictionary<Node, Message> _lastMessageSent;

        public JobMessageHandler(TranslationTable traductionTable, DistributedList distributedList) : 
        this(DependencyManager.Get<GroupViewManager>(), 
            traductionTable, distributedList,
            DependencyManager.Get<ILogger>()) {}
        public JobMessageHandler(GroupViewManager groupManager,
                          TranslationTable traductionTable, 
                          DistributedList distributedList,
                          ILogger logger)
        {
            _logger = logger;
            _traductionTable = traductionTable;
            _distributedList = distributedList;
            _groupManager = groupManager;
            _unconfirmedRequestIds = new Dictionary<Node, int>();
            _lastMessageSent = new Dictionary<Node, Message>();

            var jobPublisher = groupManager.Topics.GetPublisher<JobPublisher>();
            jobPublisher.RegisterForMessage(typeof(ExecutionRequest), OnMessageReceived);
            jobPublisher.RegisterForMessage(typeof(ExecutionResponse), OnMessageReceived);
            jobPublisher.RegisterForMessage(typeof(ExecutionAck), OnMessageReceived);
            jobPublisher.RegisterForMessage(typeof(InsertionRequest), OnMessageReceived);
            jobPublisher.RegisterForMessage(typeof(InsertionResponse), OnMessageReceived);
        }

        private bool IsMessageCorrect(Node node, Message received)
        {
            if (_lastMessageSent.ContainsKey(node))
            {
                Message lastSent = _lastMessageSent[node];
                _lastMessageSent.Remove(node);
                return received.IsTheExpectedMessage(lastSent);
            }

            _logger.Warning(Tag.JobManager, $"No message correctness checked because no message sent to node {node}");
            return true;
        }

        private void OnMessageReceived(Node node, Message message)
        {
            if (IsMessageCorrect(node, message))
            {
                if (message is ExecutionRequest executionRequest) OnExecutionRequestArrived(node, executionRequest);
                if (message is ExecutionResponse executionResponse) OnExecutionResponseArrived(node, executionResponse);
                if (message is ExecutionAck executionAck) OnExecutionAckArrived(node, executionAck);
                if (message is InsertionRequest insertionRequest) OnInsertionRequestArrived(node, insertionRequest);
                if (message is InsertionResponse insertionResponse) OnInsertionResponseArrived(node, insertionResponse);
                if (message is DistributedStorageUpdate distributedStorageUpdate) OnDistributedStorageUpdateArrived(node, distributedStorageUpdate);
            }

            _logger.Warning(Tag.JobManager, $"Received message {message} is not correct so it will be discarded");
        }        

        private void Send(Node node, Message message)
        {
            _lastMessageSent.Add(node, message);
            _groupManager.Send(node, message).Wait();
        }
        
        private void SendMulticast(Message message)
        {
            _groupManager.SendMulticast(message).Wait();
        }

        private void OnExecutionRequestArrived(Node node, ExecutionRequest message)
        {
            _logger.Log(Tag.JobManager, $"Request for an execution arrived from client {node}");
            int requestID = _traductionTable.Add(message.Job);
            _unconfirmedRequestIds.Add(node, requestID);
            Send(node, new ExecutionResponse(message, requestID));
            _logger.Log(Tag.JobManager, $"Response sent with a proposal request id ({requestID})");
        }

        // On client side
        private void OnExecutionResponseArrived(Node node, ExecutionResponse message)
        {
            // todo store id
            _logger.Log(Tag.JobManager, $"Request id stored");
            Send(node, new ExecutionAck(message, message.RequestID));
            _logger.Log(Tag.JobManager, $"Request id confirmed and sent back");
        }

        private void OnExecutionAckArrived(Node node, ExecutionAck message)
        {
            _logger.Log(Tag.JobManager, $"Ack for an execution arrived from client {node} with request id {message.RequestID}");

            // Confirm request id
            int requestID = message.RequestID;
            if (_unconfirmedRequestIds.ContainsKey(node) && _unconfirmedRequestIds[node] == requestID)
            {
                _unconfirmedRequestIds.Remove(node);
                _traductionTable.SetConfirmed(requestID);
                _logger.Log(Tag.JobManager, $"Request id confirmed");

                // Request to coordinator for an insertion
                Job job = _traductionTable.Get(requestID);
                Send(_groupManager.View.Coordinator, new InsertionRequest(job, requestID));
                _logger.Log(Tag.JobManager, $"Insertion request to coordinator for job with request id {requestID}");
            }
            else
                _logger.Warning(Tag.JobManager, $"Client request id {requestID} does not match any request id stored");
        }

        private void OnInsertionRequestArrived(Node node, InsertionRequest message)
        {
            _logger.Log(Tag.JobManager, $"Insertion request arrived from {node}");
            _distributedList.AddAndAssign(message.Job);
            _logger.Log(Tag.JobManager, $"Job added to storage and assigned");
            SendMulticast(new DistributedStorageUpdate(message.Job));
            _logger.Log(Tag.JobManager, $"Multicast sent for storage sync");
            Send(node, new InsertionResponse(message, message.Job.ID.Value, message.RequestID));
            _logger.Log(Tag.JobManager, $"Sent back to {node} the assigned id for the inserted job");
        }

        private void OnInsertionResponseArrived(Node node, InsertionResponse message)
        {
            _logger.Log(Tag.JobManager, $"Received from the coordinator the job id assigned");
            _traductionTable.SetJobID(message.RequestID, message.JobID);
            _logger.Log(Tag.JobManager, $"Translation added for request {message.RequestID} and job {message.JobID}");
        }

        private void OnDistributedStorageUpdateArrived(Node node, DistributedStorageUpdate message)
        {
            _logger.Log(Tag.JobManager, $"Storage sync message arrived");
            if (message.Job.ID.HasValue && message.Job.Node.HasValue)
            {
                _distributedList.AddOrUpdate(message.Job);
                _logger.Log(Tag.JobManager, $"Added/Updated job {message.Job.ID.Value}");
            }
            else
                _logger.Warning(Tag.JobManager, $"Arrived job is malformed (id or owner null)");
        }
    }
}