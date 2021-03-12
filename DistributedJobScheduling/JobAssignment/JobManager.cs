using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.DistributedStorage;

namespace DistributedJobScheduling.JobAssignment
{
    public class JobManager
    {
        private ICommunicationManager groupManager;
        private TraductionTable _traductionTable;

        public JobManager(ICommunicationManager groupManager, TraductionTable traductionTable)
        {
            _traductionTable = traductionTable;
            var jobPublisher = groupManager.Topics.GetPublisher<JobPublisher>();
            jobPublisher.RegisterForMessage(typeof(ExecutionRequest), OnExecutionRequestArrived);
        }

        private void OnExecutionRequestArrived(Node node, Message message)
        {
            ExecutionRequest executionRequest = (ExecutionRequest)message;
            int requestID = _traductionTable.Add(executionRequest.Job);
            ExecutionResponse response = new ExecutionResponse(executionRequest, requestID);
            groupManager.Send(node, response);
        }
    }
}