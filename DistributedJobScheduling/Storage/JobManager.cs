using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.Storage
{
    public class Jobs
    { 
        public List<Job> List;

        public Jobs() { List = new List<Job>(); }
    }

    public class JobManager : IInitializable
    {
        private ReusableIndex _reusableIndex;
        private SecureStore<Jobs> _secureStorage;
        private ILogger _logger;
        private Group _group;
        public Action<Job> UpdateJob;

        public JobManager() : this (
            DependencyInjection.DependencyManager.Get<IStore<Jobs>>(), 
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<IGroupViewManager>()) { }
        
        public JobManager(IStore<Jobs> store, ILogger logger, IGroupViewManager groupView)
        {
            _secureStorage = new SecureStore<Jobs>(store, logger);
            _reusableIndex = new ReusableIndex();
            _logger = logger;
            _group = groupView.View;
            UpdateJob += OnJobUpdateRequest;
        }

        private void OnJobUpdateRequest(Job job)
        {
            // Is allowed an update only on jobs with a reference in the storage
            if (_secureStorage.Value.List.Contains(job))
                _secureStorage.ValuesChanged.Invoke();
        }

        public void Init()
        {
            _secureStorage.Init();
            DeletePendingAndRemovedJobs();
        }

        private void DeletePendingAndRemovedJobs()
        {
            _secureStorage.Value.List.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
            _secureStorage.ValuesChanged.Invoke();
            _logger.Log(Tag.JobStorage, "Memory cleaned (logical deletions)");
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (_secureStorage.Value.List.Contains(job))
            {
                job.Status = JobStatus.REMOVED;
                _secureStorage.ValuesChanged.Invoke();
                _logger.Log(Tag.JobStorage, $"Job {job} logically removed");
            }
        }

        public void InsertAndAssign(Job job)
        {
            job.Node = FindNodeWithLessJobs();
            job.ID = _reusableIndex.NewIndex;

            _secureStorage.Value.List.Add(job);
            _secureStorage.ValuesChanged.Invoke();

            _logger.Log(Tag.JobStorage, $"Job {job} assigned to {job.Node.Value}");
        }

        public void InsertOrUpdateExternalJob(Job job)
        {
            if (!job.ID.HasValue)
            {
                _logger.Warning(Tag.JobStorage, $"External job {job} has no ID, update refused");
                return;
            }

            // If the job is already in the list it is also updated
            if (_secureStorage.Value.List.Contains(job))
            {
                _logger.Warning(Tag.JobStorage, $"External job {job} is already in the storage, update refused");
                return;
            }

            // Remove job with same ID
            foreach (Job stored in _secureStorage.Value.List)
                if (stored.ID.HasValue && stored.ID.Value == job.ID.Value)
                    _secureStorage.Value.List.Remove(stored);
                
            _secureStorage.Value.List.Add(job);
            _secureStorage.ValuesChanged.Invoke();
        }

        public Dictionary<int, int> FindNodesOccurrences()
        {
            // Init each node with no occurrences
            Dictionary<int, int> nodeJobCount = new Dictionary<int, int>();
            nodeJobCount.Add(_group.Me.ID.Value, 0);
            nodeJobCount.Add(_group.Coordinator.ID.Value, 0);
            _group.Others.ForEach(node => nodeJobCount.Add(node.ID.Value, 0));

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

            _logger.Log(Tag.JobStorage, $"Total job assigned per node: {nodeJobCount.ToString()}");
            return nodeJobCount;
        }

        private int FindNodeWithLessJobs()
        {
            Dictionary<int, int> nodeJobCount = FindNodesOccurrences();

            // Find the node with the less number of assignment
            (int, int) min = (_group.Me.ID.Value, nodeJobCount[_group.Me.ID.Value]);
            nodeJobCount.ForEach(nodeOccurencesPair => 
            {
                if (nodeOccurencesPair.Value < min.Item2)
                    min = (nodeOccurencesPair.Key, nodeOccurencesPair.Value);
            });

            _logger.Log(Tag.JobStorage, $"Node with less occurrences: {min.Item1} with {min.Item2} jobs to do");
            return min.Item1;
        }

        public Job FindJobToExecute()
        {
            Job toExecute = null;
            _secureStorage.Value.List.ForEach(job => 
            {
                if (job.Status == JobStatus.PENDING && job.Node == _group.Me.ID)
                    toExecute = job;
            });
            return toExecute;
        }
    }
}