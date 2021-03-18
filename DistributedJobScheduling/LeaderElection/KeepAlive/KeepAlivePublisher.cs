using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Messaging.LeaderElection.KeepAlive;
using DistributedJobScheduling.Communication.Topics;

namespace DistributedJobScheduling.LeaderElection.KeepAlive
{
    public class KeepAlivePublisher : BaseTopicPublisher
    {
        private HashSet<Type> _topics = new HashSet<Type>
        {
            typeof(KeepAliveRequest),
            typeof(KeepAliveResponse)
        };
        public override HashSet<Type> TopicMessageTypes => _topics;
    }
}