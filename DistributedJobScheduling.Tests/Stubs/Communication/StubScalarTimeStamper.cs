using DistributedJobScheduling.Communication.Messaging;

namespace DistributedJobScheduling.Tests.Communication.Messaging
{
    public class StubScalarTimeStamper : ITimeStamper
    {
        private int _idCount = 0;
        private Node _node;

        public StubScalarTimeStamper(Node node)
        {
            _node = node;
        }

        public int CreateTimeStamp()
        {
            return _idCount++;
        }
    }
}