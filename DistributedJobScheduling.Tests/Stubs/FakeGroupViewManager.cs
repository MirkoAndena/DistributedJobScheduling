using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.Tests
{
    public class FakeGroupViewManager : IGroupViewManager
    {
        private Group _view;
        public Group View => _view;

        public ITopicOutlet Topics => null;

        public event Action<Node, Message> OnMessageReceived;

        public FakeGroupViewManager(Group group)
        {
            _view = group;
        }

        public Task Send(Node node, Message message, int timeout = 30)
        {
            return null;
        }

        public Task SendMulticast(Message message)
        {
            return null;
        }
    }
}