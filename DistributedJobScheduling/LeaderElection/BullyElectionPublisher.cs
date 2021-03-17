using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Messaging.LeaderElection;
using DistributedJobScheduling.Communication.Topics;

namespace DistributedJobScheduling.LeaderElection
{
    public class BullyElectionPublisher : BaseTopicPublisher
    {
        private HashSet<Type> _topics = new HashSet<Type>
        {
            typeof(ElectMessage),
            typeof(CoordMessage)
        };
        public override HashSet<Type> TopicMessageTypes => _topics;
    }
}