using System;
using DistributedJobScheduling.Communication.Basic;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging.Discovery
{
    [JsonObject(MemberSerialization.Fields)]
    [Serializable]
    public class NetworkStateMessage : Message 
    {
        public Node[] Nodes { get; private set; }
        
        [JsonConstructor]
        public NetworkStateMessage(Node[] nodes) : base() 
        {
            Nodes = nodes;
        }

        public override void BindToRegistry(Node.INodeRegistry registry)
        {
            base.BindToRegistry(registry);

            Node[] newNodes = new Node[Nodes.Length];
            for(int i = 0; i < Nodes.Length; i++)
                newNodes[i] = registry.GetOrCreate(Nodes[i]);
            Nodes = newNodes;
        }
    }
}