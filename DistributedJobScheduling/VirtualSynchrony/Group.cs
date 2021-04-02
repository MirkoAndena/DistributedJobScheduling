using System.Net;
using System.Collections.Generic;
using System;
using DistributedJobScheduling.Communication.Basic;
using System.Threading.Tasks;

namespace DistributedJobScheduling.VirtualSynchrony
{
    public class Group
    {
        /// <summary>
        /// Event notified when the current group view changes
        /// </summary>
        public event Action ViewChanged;
        public Node Me { get; private set; }
        public Node Coordinator  { get; private set; }
        public HashSet<Node> Others  { get; private set; }

        public bool ImCoordinator => Me == Coordinator;
        public int Count
        {
            get {
                lock(this)
                {
                    return Others.Count + 1;
                }
            }
        }

        public Group(Node me, bool coordinator = false) 
        {
            Me = me;

            if (coordinator)
                Coordinator = Me;

            Others = new HashSet<Node>();
        }

        public void Add(Node node)
        {
            lock(this)
            {
                Others.Add(node);
            }
            Task.Run(() => ViewChanged?.Invoke());
        }

        public void UpdateCoordinator(Node node)
        {
            lock(this)
            {
                Coordinator = node;
            }
            Task.Run(() => ViewChanged?.Invoke());
        }

        public void Update(HashSet<Node> newView, Node newCoordinator)
        {
            lock(this)
            {
                Others = newView;
                Coordinator = newCoordinator;
            }
            Task.Run(() => ViewChanged?.Invoke());
        }

        public void Remove(Node node)
        {
            lock(this)
            {
                if (Coordinator == node)
                    Coordinator = null;
                Others.Remove(node);
            }

            Task.Run(() => ViewChanged?.Invoke());
        }

        public bool Contains(Node node)
        {
            lock(this)
            {
                return node == Me || Others.Contains(node);
            }
        }

        public bool Contains(HashSet<Node> nodes)
        {
            HashSet<Node> toCheck = new HashSet<Node>(nodes);
            toCheck.Remove(Me);
            lock(this)
            {
                return toCheck.IsSubsetOf(Others);
            }
        }
    }
}