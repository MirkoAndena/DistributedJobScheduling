using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Message sent to nodes we detect are sending messages thinking they are in a view, when they shouldn't be
    /// It doesn't completely solve partitioning, even though it is assumed that no partitioning can occur
    /// 
    /// If the receiving view has more members it will teardown this one
    /// If the receiving view has less members they'll teardown
    /// Data might be lost in this operation (this situation is assumed to never occur)
    /// </summary>
    public class NotInViewMessage : Message
    {
        public int MyViewSize { get; private set; }
        
        [JsonConstructor]
        public NotInViewMessage(int myViewSize) : base()
        {
            MyViewSize = myViewSize;
        }
    }
}