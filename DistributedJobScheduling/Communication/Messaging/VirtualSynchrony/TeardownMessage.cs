using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Message sent to views that know are not the main one and should be dismantled
    /// </summary>
    public class TeardownMessage : Message
    {
        public TeardownMessage() : base()
        {
        }
    }
}