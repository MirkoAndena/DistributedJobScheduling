using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.DistributedStorage.SecureStorage;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.DistributedStorage
{
    public class Jobs
    { 
        public List<Job> List;

        public Jobs() { List = new List<Job>(); }
    }

    public class DistributedList : IMemoryCleaner
    {
        private ReusableIndex _reusableIndex;
        private SecureStore<Jobs> _secureStorage;
        public Action<Job, IJobResult> OnJobCompleted;

        public List<Job> Values => _secureStorage.Value.List;

        public DistributedList() { _secureStorage = new SecureStore<Jobs>(); }
        public DistributedList(IStore store)
        {
            _secureStorage = new SecureStore<Jobs>(store);
            _reusableIndex = new ReusableIndex();
        }

        public void CleanLogicRemoved() => DeletePendingAndRemovedJobs();
        private void DeletePendingAndRemovedJobs()
        {
            _secureStorage.Value.List.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
            _secureStorage.ValuesChanged.Invoke();
        }

        public void Add(Job job)
        {        
            _secureStorage.Value.List.Add(job);
            _secureStorage.ValuesChanged.Invoke();
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (_secureStorage.Value.List.Contains(job))
            {
                job.Status = JobStatus.REMOVED;
                _secureStorage.ValuesChanged.Invoke();
            }
        }

        public void AddAndAssign(Job job, Group group)
        {
            job.Node = FindNodeWithLessJobs(group);
            job.ID = _reusableIndex.NewIndex;

            _secureStorage.Value.List.Add(job);
            _secureStorage.ValuesChanged.Invoke();
        }

        private int FindNodeWithLessJobs(Group group)
        {
            // Init each node with no occurrences
            Dictionary<int, int> nodeJobCount = new Dictionary<int, int>();
            nodeJobCount.Add(group.Me.ID.Value, 0);
            nodeJobCount.Add(group.Coordinator.ID.Value, 0);
            group.Others.ForEach(node => nodeJobCount.Add(node.ID.Value, 0));

            // For each node calculate how many jobs are assigned
            foreach (Job job in _secureStorage.Value.List)
            {
                // Here should be always true
                if (job.Node.HasValue)
                {
                    if (nodeJobCount.ContainsKey(job.Node.Value))
                        nodeJobCount[job.Node.Value]++;
                    else
                        nodeJobCount.Add(job.Node.Value, 1);
                }
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

                    IJobResult result = await current.Run();
                    
                    current.Status = JobStatus.COMPLETED;
                    _secureStorage.ValuesChanged.Invoke();
                    OnJobCompleted?.Invoke(current, result);
                }
            }
        }

        private Job FindJobToExecute(Node me)
        {
            Job toExecute = null;
            _secureStorage.Value.List.ForEach(job => 
            {
                if (job.Status == JobStatus.PENDING && job.Node == me.ID)
                    toExecute = job;
            });
            return toExecute;
        }
    }
}