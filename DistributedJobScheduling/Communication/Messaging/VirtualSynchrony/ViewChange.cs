using System.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.VirtualSynchrony;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Messaging
{
    [Serializable]
    public class ViewChange : IRegistryBindable
    {
        [JsonProperty]
        public int? ViewId { get; set; }

        [JsonIgnore]
        [field: NonSerialized]
        [IgnoreDataMemberAttribute]
        private bool _isBound;

        /// <value>Node that changed, WARNING: Don't use this instance to communicate use the NodeRegistry</value>
        [JsonIgnore]
        [field: NonSerialized]
        [IgnoreDataMemberAttribute]
        public Dictionary<Node, Operation> Changes { get; set; }

        [JsonProperty]
        public List<KeyValuePair<Node, Operation>> SerializedChanges
        {
            get { return Changes?.ToList(); }
            set { Changes = value.ToDictionary(x => x.Key, x => x.Value); }
        }
        
        public bool IsSame(ViewChange other) => other.ViewId == this.ViewId;

        public Dictionary<Node, Operation> Difference(ViewChange other)
        {
            if(!this._isBound || !other._isBound)
                throw new InvalidOperationException("Trying to merge ViewChanges that aren't bound to the local registry");
            
            var differences = new Dictionary<Node, Operation>();
            foreach(var node in other.Changes.Keys)
            {
                if(!Changes.ContainsKey(node))
                    differences.Add(node, other.Changes[node]);
                else if (other.Changes[node] != Changes[node])
                    throw new InvalidOperationException($"ViewChange diff has inconsistencies for node {node}");
            }
            return differences;
        }

        public void Merge(ViewChange other)
        {
            if(!this._isBound || !other._isBound)
                throw new InvalidOperationException("Trying to merge ViewChanges that aren't bound to the local registry");
                
            foreach(var node in other.Changes.Keys)
            {
                if(!Changes.ContainsKey(node))
                    Changes.Add(node, other.Changes[node]);
                else if (other.Changes[node] != Changes[node])
                    throw new InvalidOperationException($"Trying to merge ViewChanges with inconsistent operations for node {node}");
            }
        }

        public HashSet<Node> Apply(HashSet<Node> currentNodes, bool onlyLeft = false)
        {
            var resultSet = new HashSet<Node>(currentNodes);
            foreach(var node in Changes.Keys)
            {
                switch(Changes[node])
                {
                    case Operation.Joined:
                        if(!currentNodes.Contains(node) && !onlyLeft)
                            resultSet.Add(node);
                        break;
                    case Operation.Left:
                        if(resultSet.Contains(node))
                            resultSet.Remove(node);
                        break;
                }
            }
            return resultSet;
        }

        public void BindToRegistry(Node.INodeRegistry registry)
        {
            foreach(Node nodeKey in Changes.Keys.ToArray())
            {
                Node boundNode = registry.GetOrCreate(nodeKey);
                if(nodeKey != boundNode)
                {
                    Operation operation = Changes[nodeKey];
                    Changes.Remove(nodeKey);
                    Changes.Add(boundNode, operation);
                }
            }
            _isBound = true;
        }

        public override string ToString()
        {
            return $"{ViewId} : [{string.Join(',', Changes.Select(x => $"{{'{x.Key}' : { x.Value }}} "))}]";
        }
    }

    public enum Operation
    {
        Joined,
        Left
    }
}