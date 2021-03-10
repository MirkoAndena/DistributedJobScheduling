using System.Net;
using System.Collections.Generic;
using System;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.VirtualSynchrony
{
    public class Group
    {
        public Node Me { get; private set; }
        public Node Coordinator  { get; private set; }
        public HashSet<Node> Others  { get; private set; }

        public Group(Node me, bool coordinator = false) 
        {
            Me = me;

            if (coordinator)
                Coordinator = Me;

            Others = new HashSet<Node>();
        }

        public void Add(Node node) => Others.Add(node);
        public void UpdateCoordinator(Node node) => Coordinator = node;

        public void Remove(Node node)
        {
            if (Coordinator == node)
                Coordinator = null;
            Others.Remove(node);
        }
    }
}