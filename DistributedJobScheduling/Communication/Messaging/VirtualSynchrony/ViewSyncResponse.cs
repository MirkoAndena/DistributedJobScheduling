using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Response to a ViewJoinRequest, sent by the coordinator once the join viewchange was handled
    /// </summary>
    public class ViewSyncResponse : Message
    {
        public List<Node> ViewNodes { get; private set; }

        public ViewSyncResponse(List<Node> viewNodes) : base() 
        {
            ViewNodes = viewNodes;
        }
        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);
            List<Node> boundNodes = new List<Node>();
            ViewNodes.ForEach(n => boundNodes.Add(registry.GetOrCreate(n)));
            ViewNodes = boundNodes;
        }
    }
}