using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.Communication.Topics;

namespace DistributedJobScheduling.JobAssignment
{
    public class JobPublisher : BaseTopicPublisher
    {
        private HashSet<Type> _topics = new HashSet<Type>
        {
            typeof(AssignmentMessage),
            typeof(ExecutionRequest),
            typeof(ExecutionResponse),
            typeof(ExecutionAck),
            typeof(InsertionRequest),
            typeof(InsertionResponse)
        };
        public override HashSet<Type> TopicMessageTypes => _topics;
    }
}