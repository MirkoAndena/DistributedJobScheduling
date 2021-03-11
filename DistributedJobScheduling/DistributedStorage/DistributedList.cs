using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.DistributedStorage.SecureStorage;
using DistributedJobScheduling.JobAssignment.Job;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.DistributedStorage
{
    public class DistributedList
    {
        private int _jobIdCount = 0;
        private Action<Job> OnJobAssigned;
        private SecureStore _secureStorage;

        public List<Job> Values => _secureStorage.Values;

        public DistributedList() { _secureStorage = new SecureStore(); }
        public DistributedList(IStore store)
        {
            _secureStorage = new SecureStore(store);
        }

        public void DeletePendingAndRemovedJobs()
        {
            _secureStorage.Values.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
            _secureStorage.ValuesChanged.Invoke();
        }

        public void Add(Job job)
        {        
            _secureStorage.Values.Add(job);
            _secureStorage.ValuesChanged.Invoke();
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (_secureStorage.Values.Contains(job))
            {
                job.Status = JobStatus.REMOVED;
                _secureStorage.ValuesChanged.Invoke();
            }
        }

        public void AddAndAssign(Job job, Group group)
        {
            job.Node = FindNodeWithLessJobs(group);
            job.ID = _jobIdCount++;

            _secureStorage.Values.Add(job);
            _secureStorage.ValuesChanged.Invoke();
            
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
            foreach (Job job in _secureStorage.Values)
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
                    _secureStorage.ValuesChanged.Invoke();

                    await current.Run();
                    
                    current.Status = JobStatus.COMPLETED;
                    _secureStorage.ValuesChanged.Invoke();
                }
            }
        }

        private Job FindJobToExecute(Node me)
        {
            Job toExecute = null;
            _secureStorage.Values.ForEach(job => 
            {
                if (job.Status == JobStatus.PENDING && job.Node == me.ID)
                    toExecute = job;
            });
            return toExecute;
        }
    }
}