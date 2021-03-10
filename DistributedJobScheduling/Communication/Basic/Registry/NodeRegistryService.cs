using System.Collections.Generic;
namespace DistributedJobScheduling.Communication.Basic
{
    public partial class Node
    {
        //TODO: Check constraints? Unique IP and NodeID?
        public class NodeRegistryService : INodeRegistry
        {
            private HashSet<Node> _localNodes = new HashSet<Node>();
            private Dictionary<int, Node> _nodeIDMap = new Dictionary<int, Node>();
            private Dictionary<string, Node> _nodeIPMap = new Dictionary<string, Node>();

            public Node GetNode(int ID)
            {
                if(_nodeIDMap.ContainsKey(ID))
                    return _nodeIDMap[ID];
                else
                    return null;
            }

            public Node GetNode(string IP)
            {
                if(_nodeIPMap.ContainsKey(IP))
                    return _nodeIPMap[IP];
                else
                    return null;
            }

            public Node GetOrCreate(Node node) => GetOrCreate(node.IP, node.ID);
            public Node GetOrCreate(string ip, int? id = null)
            {
                Node nodeToReturn = null;
                if(id.HasValue && _nodeIDMap.ContainsKey(id.Value))
                    nodeToReturn = _nodeIDMap[id.Value];
                if(ip != null && _nodeIPMap.ContainsKey(ip))
                    nodeToReturn = _nodeIPMap[ip];
                if(nodeToReturn == null)
                {
                    nodeToReturn = new Node(ip, id);
                    _localNodes.Add(nodeToReturn);
                    if(ip != null) _nodeIPMap.Add(ip, nodeToReturn);
                    if(id.HasValue) _nodeIDMap.Add(id.Value, nodeToReturn);
                }

                return nodeToReturn;
            }

            public void UpdateNodeID(Node node, int newID)
            {
                if(!_localNodes.Contains(node))
                    throw new System.Exception("Tried to change a non local node!");

                if(node.ID == newID)
                    return;

                if(node.ID.HasValue)
                    _nodeIDMap.Remove(node.ID.Value);
                node.ID = newID;
                _nodeIDMap.Add(newID, node);
            }

            public void UpdateNodeIP(Node node, string newIP)
            {
                if(!_localNodes.Contains(node))
                    throw new System.Exception("Tried to change a non local node!");

                if(node.IP == newIP)
                    return;

                if(node.IP != null)
                    _nodeIPMap.Remove(node.IP);
                node.IP = newIP;
                _nodeIPMap.Add(newIP, node);
            }
        }
    }
}