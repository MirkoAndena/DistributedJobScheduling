using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.DistributedStorage
{
    public class DistributedList
    {
        private int _jobIdCount = 0;
        private Action<Job> OnJobAssigned;
        private SecureStorage _secureStorage;

        public List<Job> Values => _secureStorage.Value;

        public DistributedList() { _secureStorage = new SecureStorage(); }
        public DistributedList(IStore store)
        {
            _secureStorage = new SecureStorage(store);
        }

        public void DeletePendingAndRemovedJobs()
        {
            _secureStorage.Value.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
            _secureStorage.ValueChanged.Invoke();
        }

        public void Add(Job job)
        {        
            _secureStorage.Value.Add(job);
            _secureStorage.ValueChanged.Invoke();
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (_secureStorage.Value.Contains(job))
            {
                job.Status = JobStatus.REMOVED;
                _secureStorage.ValueChanged.Invoke();
            }
        }

        public void AddAndAssign(Job job, Group group)
        {
            job.Node = FindNodeWithLessJobs(group);
            job.ID = _jobIdCount++;

            _secureStorage.Value.Add(job);
            _secureStorage.ValueChanged.Invoke();
            
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
            foreach (Job job in _secureStorage.Value)
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
                {
                    current.Status = JobStatus.RUNNING;
                    _secureStorage.ValueChanged.Invoke();

                    await current.Run();
                    
                    current.Status = JobStatus.COMPLETED;
                    _secureStorage.ValueChanged.Invoke();
                }
            }
        }

        private Job FindJobToExecute(Node me)
        {
            Job toExecute = null;
            _secureStorage.Value.ForEach(job => 
            {
                if (job.Status == JobStatus.PENDING && job.Node == me.ID)
                    toExecute = job;
            });
            return toExecute;
        }
    }
}