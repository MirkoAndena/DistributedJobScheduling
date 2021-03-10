using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    public class ViewChangeMessage : Message
    {
        public enum ViewChangeOperation
        {
            Joined,
            Left
        }

        /// <value>Node that changed, WARNING: Don't use this instance to communicate use the NodeRegistry</value>
        public Node Node { get; private set; }
        public ViewChangeOperation ViewChange { get; private set; }
    }
}