namespace DistributedJobScheduling.Communication.Messaging
{
    public class ScalarTimeStamper : ITimeStamper
    {
        private static int _idCount = 0;
        public string CreateTimeStamp()
        {
            return $"{Workers.Instance.Me.ID}_{_idCount++}";
        }
    }
}