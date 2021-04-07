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
        public event Action<Node> MemberDied;
        public int? ViewId { get; private set; }
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

        public Group(Node me, bool coordinator = false, HashSet<Node> others = null, int? id = 0) : this(me, coordinator ? me : null, others, id) { }
        public Group(Node me, Node coordinator, HashSet<Node> others = null, int? id = 0)
        {
            Me = me;
            ViewId = id;
            Coordinator = coordinator;
            Others = others != null ? new HashSet<Node>(others) : new HashSet<Node>();
        }

        public void Add(Node node, int? id = null)
        {
            lock(this)
            {
                Others.Add(node);
                node.Died += MemberDied;

                if(id.HasValue) ViewId = id.Value;
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

        public void Update(HashSet<Node> newView, Node newCoordinator, int? id = null)
        {
            lock(this)
            {
                foreach (var node in Others)
                    node.Died -= OnMemberDeath;

                Others = newView;

                foreach (var node in Others)
                    node.Died += OnMemberDeath;

                Coordinator = newCoordinator;

                if(id.HasValue) ViewId = id.Value;
            }
            Task.Run(() => ViewChanged?.Invoke());
        }

        private void OnMemberDeath(Node node)
        {
            MemberDied?.Invoke(node);
        }

        public void Remove(Node node,  int? id = null)
        {
            lock(this)
            {
                if (Coordinator == node)
                    Coordinator = null;
                node.Died -= OnMemberDeath;
                Others.Remove(node);

                if(id.HasValue) ViewId = id.Value;
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
    
        /// <summary>
        /// Copies the current view state in a new object
        /// </summary>
        public Group Diverge() => new Group(Me, Coordinator, Others, ViewId);
    }
}