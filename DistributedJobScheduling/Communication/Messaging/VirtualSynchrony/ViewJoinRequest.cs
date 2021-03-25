using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Request a group to join, processed only by the coordinator
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ViewJoinRequest : Message
    {
        public Node JoiningNode { get; private set; }

        [JsonConstructor]
        public ViewJoinRequest(Node joiningNode) : base() 
        {
            JoiningNode = joiningNode;
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            JoiningNode = registry.GetOrCreate(JoiningNode);
        }
    }
}