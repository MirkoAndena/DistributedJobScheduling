using System.Net;
using System.Collections.Generic;
using System;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.VirtualSynchrony
{
    public class Group
    {
        private Dictionary<int, Node> _others;
        private Node _me;
        private Node _coordinator;

        public Group(int id, bool coordinator = false) 
        {
            string myip = null;
            _me = new Node(myip, id);

            if (coordinator)
                _coordinator = _me;
        }

        public Node Me => _me;
        public Node Coordinator => _coordinator;
        public Dictionary<int, Node> Others => _others;
    }
}