using System.Net;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;

namespace DistributedJobScheduling
{
    public class Node
    {
        public string IP;
        public int ID;
        public bool Coordinator;

        public Node(string ip, int id, bool coordinator)
        {
            this.IP = ip;
            this.ID = id;
            this.Coordinator = coordinator;
        }

        public override string ToString() => $"{ID} ({IP})";
    }

    class StoredGroup
    { 
        public List<Node> Nodes; 

        public StoredGroup(List<Node> nodes)
        {
            this.Nodes = nodes;
        }
    }

    public class Workers
    {
        public static Workers Instance { get; private set; }

        private Dictionary<int, Node> _others;
        private Node _me;
        private Node _coordinator;

        private Workers(List<Node> nodes, int myID) 
        {
            _others = new Dictionary<int, Node>();
            nodes.ForEach(node => 
            {
                if (node.ID == myID) _me = node;
                else if (node.Coordinator) _coordinator = node;
                else _others.Add(node.ID, node);
            });
        }

        private static List<Node> ReadFromJson(string jsonPath)
        {
            string json = File.ReadAllText(jsonPath);
            StoredGroup stored = JsonConvert.DeserializeObject<StoredGroup>(json);
            return stored.Nodes;
        }
        
        public static Workers Build(string groupJsonFile, int myID)
        {
            Workers.Instance = new Workers(ReadFromJson(groupJsonFile), myID);
            return Workers.Instance;
        }

        public Node Me => _me;
        public Node Coordinator => _coordinator;
        public Dictionary<int, Node> Others => _others;

        public static Node SearchFromIP(EndPoint endPoint)
        {
            string ip = ((IPEndPoint)endPoint).Address.ToString();
            if (ip == Workers.Instance.Coordinator.IP) return Workers.Instance.Coordinator;
            foreach (Node node in Workers.Instance.Others.Values) 
                if (ip == node.IP)
                    return node;
            throw new Exception($"Received a connection request from someone that's not in the group: ${ip}");
        }
    }
}