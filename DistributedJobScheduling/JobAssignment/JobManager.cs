using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.DistributedStorage;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.JobAssignment
{
    public class JobManager
    {
        private GroupViewManager _groupManager;
        private TranslationTable _traductionTable;
        private DistributedList _distributedList;
        
        private Dictionary<Node, int> _unconfirmedRequestIds;
        private Dictionary<Node, Message> _lastMessageSent;

        public JobManager(TranslationTable traductionTable, DistributedList distributedList) : this(DependencyManager.Get<GroupViewManager>(), traductionTable, distributedList) {}
        public JobManager(GroupViewManager groupManager, TranslationTable traductionTable, DistributedList distributedList)
        {
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
        }        

        private void Send(Node node, Message message)
        {
            _lastMessageSent.Add(node, message);
            _groupManager.Send(node, message);
        }
        
        private void SendMulticast(Message message)
        {
            _groupManager.SendMulticast(message);
        }

        private void OnExecutionRequestArrived(Node node, ExecutionRequest message)
        {
            int requestID = _traductionTable.Add(message.Job);
            _unconfirmedRequestIds.Add(node, requestID);
            Send(node, new ExecutionResponse(message, requestID));
        }

        // On client side
        private void OnExecutionResponseArrived(Node node, ExecutionResponse message)
        {
            // todo store id
            Send(node, new ExecutionAck(message, message.RequestID));
        }

        private void OnExecutionAckArrived(Node node, ExecutionAck message)
        {
            // Confirm request id
            int requestID = message.RequestID;
            if (_unconfirmedRequestIds.ContainsKey(node) && _unconfirmedRequestIds[node] == requestID)
                _unconfirmedRequestIds.Remove(node);
            _traductionTable.SetConfirmed(requestID);

            // Request to coordinator for an insertion
            Job job = _traductionTable.Get(requestID);
            Send(_groupManager.View.Coordinator, new InsertionRequest(job, requestID));
        }

        private void OnInsertionRequestArrived(Node node, InsertionRequest message)
        {
            _distributedList.AddAndAssign(message.Job, _groupManager.View);
            SendMulticast(new DistributedStorageUpdate(message.Job));
        }

        private void OnInsertionResponseArrived(Node node, InsertionResponse message)
        {
            _traductionTable.SetJobID(message.RequestID, message.JobID);
        }

        private void OnDistributedStorageUpdateArrived(Node node, DistributedStorageUpdate distributedStorageUpdate)
        {
            _distributedList.AddOrUpdate(distributedStorageUpdate.Job);
        }
    }
}