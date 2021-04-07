using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ViewChange
    {
        /// <value>Node that changed, WARNING: Don't use this instance to communicate use the NodeRegistry</value>
        public Node Node { get; set; }
        public int? ViewId { get; set; }
        public ViewChangeOperation Operation { get; set; }
        
        public bool IsSame(ViewChange other) => IsSame(other.Node, other.Operation, other.ViewId);
        public bool IsSame(Node node, ViewChangeOperation operation, int? viewId)
        {
            return Node.ID == node.ID && Operation == operation && ViewId == viewId;
        }

        public void BindToRegistry(Node.INodeRegistry registry)
        {
            Node = registry.GetOrCreate(Node);
        }
    }

    public enum ViewChangeOperation
    {
        Joined,
        Left
    }
}