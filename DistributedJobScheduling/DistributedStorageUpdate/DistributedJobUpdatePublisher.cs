using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;
using DistributedJobScheduling.Communication.Topics;

namespace DistributedJobScheduling.DistributedJobUpdate
{
    public class DistributedJobUpdatePublisher : BaseTopicPublisher
    {
        private HashSet<Type> _topics = new HashSet<Type>
        {
            typeof(DistributedStorageUpdateRequest),
            typeof(DistributedStorageUpdate)
        };
        public override HashSet<Type> TopicMessageTypes => _topics;
    }
}