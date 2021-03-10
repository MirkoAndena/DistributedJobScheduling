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

        public DistributedList() { }

        public void DeletePendingAndRemovedJobs()
        {
            SecureStorage.Instance.Value.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
            SecureStorage.Instance.ValueChanged.Invoke();
        }

        public void Add(Job job)
        {        
            SecureStorage.Instance.Value.Add(job);
            SecureStorage.Instance.ValueChanged.Invoke();
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (SecureStorage.Instance.Value.Contains(job))
            {
                job.Status = JobStatus.REMOVED;
                SecureStorage.Instance.ValueChanged.Invoke();
            }
        }

        public void AddAndAssign(Job job, Group group)
        {
            job.Node = FindNodeWithLessJobs(group);
            job.ID = _jobIdCount++;

            SecureStorage.Instance.Value.Add(job);
            SecureStorage.Instance.ValueChanged.Invoke();
            
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
            foreach (Job job in SecureStorage.Instance.Value)
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
                    SecureStorage.Instance.ValueChanged.Invoke();

                    await current.Run();
                    
                    current.Status = JobStatus.COMPLETED;
                    SecureStorage.Instance.ValueChanged.Invoke();
                }
            }
        }

        private Job FindJobToExecute(Node me)
        {
            Job toExecute = null;
            SecureStorage.Instance.Value.ForEach(job => 
            {
                if (job.Status == JobStatus.PENDING && job.Node == me.ID)
                    toExecute = job;
            });
            return toExecute;
        }
    }
}