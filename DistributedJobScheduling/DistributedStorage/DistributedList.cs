using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.DistributedStorage
{
    public class DistributedList
    {
        private int _jobIdCount = 0;
        private List<Job> _value;
        private Action<Job> OnJobAssigned;
        private Action<Job> OnJobStatusChanged;

        public List<Job> Value => _value;

        public DistributedList()
        {
            _value = new List<Job>();
        }

        public void DeletePendingAndRemovedJobs()
        {
            _value.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
        }

        public void Add(Job job)
        {
            job.OnStatusChanged += _OnJobStatusChanged;
            _value.Add(job);
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (_value.Contains(job))
            {
                job.OnStatusChanged -= _OnJobStatusChanged;
                job.SetRemoved();
            }
        }

        private void _OnJobStatusChanged(Job job)
        {
            OnJobStatusChanged?.Invoke(job);
        }

        public void AddAndAssign(Job job, Group group)
        {
            job.Node = FindNodeWithLessJobs(group);
            job.ID = _jobIdCount++;
            _value.Add(job);
            OnJobAssigned?.Invoke(job);
        }

        private int FindNodeWithLessJobs(Group group)
        {
            // Init each node with no occurrences
            Dictionary<int, int> nodeJobCount = new Dictionary<int, int>();
            nodeJobCount.Add(group.Me.ID.Value, 0);
            nodeJobCount.Add(group.Coordinator.ID.Value, 0);
            group.Others.ForEach(node => nodeJobCount.Add(node.ID.Value, 0));

            // For each node calculate how many jobs are assigned
            foreach (Job job in _value)
            {
                if (nodeJobCount.ContainsKey(job.Node))
                    nodeJobCount[job.Node]++;
                else
                    nodeJobCount.Add(job.Node, 1);
            }

            // Find the node with the less number of assignment
            (int, int) min = (group.Me.ID.Value, nodeJobCount[group.Me.ID.Value]);
            nodeJobCount.ForEach((node, occurrences) => 
            {
                if (occurrences < min.Item2)
                    min = (node, occurrences);
            });

            return min.Item1;
        }

        public async void RunAssignedJob(Group group)
        {
            while (true)
            {
                Job current = FindJobToExecute(group.Me);
                if (current != null)
                    await current.Run();
            }
        }

        private Job FindJobToExecute(Node me)
        {
            Job toExecute = null;
            _value.ForEach(job => 
            {
                if (job.Status == JobStatus.PENDING && job.Node == me.ID)
                    toExecute = job;
            });
            return toExecute;
        }
    }
}