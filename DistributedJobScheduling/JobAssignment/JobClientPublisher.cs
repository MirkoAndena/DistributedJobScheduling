using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.Communication.Topics;

namespace DistributedJobScheduling.JobAssignment
{
    public class JobClientPublisher : BaseTopicPublisher
    {
        private HashSet<Type> _topics = new HashSet<Type>
        {
            typeof(ExecutionRequest),
            typeof(ExecutionResponse),
            typeof(ExecutionAck),
            typeof(ResultRequest),
            typeof(ResultResponse)
        };
        public override HashSet<Type> TopicMessageTypes => _topics;
    }
}