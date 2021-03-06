using System.Net;
using System.Collections.Generic;
using System;

namespace DistributedJobScheduling.VirtualSynchrony
{
    public class Group
    {
        private Dictionary<int, Node> _others;
        private Node _me;
        private Node _coordinator;

        public Group(int id, bool coordinator = false) 
        {
            _me = new Node() { ID = id, IP = "" };
        }

        public Node Me => _me;
        public Node Coordinator => _coordinator;
        public Dictionary<int, Node> Others => _others;

        public static Node SearchFromIP(EndPoint endPoint)
        {
            string ip = ((IPEndPoint)endPoint).Address.ToString();
            if (ip == Group.Instance.Coordinator.IP) return Group.Instance.Coordinator;
            foreach (Node node in Group.Instance.Others.Values) 
                if (ip == node.IP)
                    return node;
            throw new Exception($"Received a connection request from someone that's not in the group: ${ip}");
        }
    }
}