using System.Net;
using System.Collections.Generic;
using System;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.VirtualSynchrony
{
    public class Group
    {
        private List<Node> _others;
        private Node _me;
        private Node _coordinator;

        public Group(int id, bool coordinator = false) 
        {
            string myip = null;
            _me = new Node(myip, id);

            if (coordinator)
                _coordinator = _me;

            _others = new List<Node>();
        }

        public Node Me => _me;
        public Node Coordinator => _coordinator;
        public List<Node> Others => _others;

        public void Add(Node node) => _others.Add(node);
        public void AddCoordinator(Node node) => _coordinator = node;

        public void Remove(Node node)
        {
            if (_coordinator == node)
                _coordinator = null;
            if (_others.Contains(node))
                _others.Remove(node);
        }
    }
}