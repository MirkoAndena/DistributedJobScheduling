using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;

namespace DistributedJobScheduling.Communication.Topics
{
    public class VirtualSynchronyTopicPublisher : BaseTopicPublisher
    {
        private HashSet<Type> _topics = new HashSet<Type>
        {
            typeof(TemporaryMessage),
            typeof(TemporaryAckMessage),
            typeof(ViewChangeMessage),
            typeof(FlushMessage),
            typeof(ViewJoinRequest),
            typeof(ViewSyncResponse),
            typeof(NotInViewMessage),
            typeof(TeardownMessage)
        };
        public override HashSet<Type> TopicMessageTypes => _topics;
    }
}