using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    /// <summary>
    /// Response to a ViewJoinRequest, sent by the coordinator once the join viewchange was handled
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class ViewSyncResponse : Message
    {
        public List<Node> ViewNodes { get; private set; }
        public int ViewId { get; private set; }

        [JsonConstructor]
        public ViewSyncResponse(List<Node> viewNodes, int viewId) : base() 
        {
            ViewNodes = viewNodes;
            ViewId = viewId;
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