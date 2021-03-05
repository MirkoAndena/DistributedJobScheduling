namespace DistributedJobScheduling.Communication.Messaging
{
    public class ScalarTimeStamper : ITimeStamper
    {
        private static int _idCount = 0;

        public int CreateTimeStamp()
        {
            return _idCount++;
        }
    }
}