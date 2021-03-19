using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.DistributedStorage.SecureStorage;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.DistributedStorage
{
    public class Jobs
    { 
        public List<Job> List;

        public Jobs() { List = new List<Job>(); }
    }

    public class DistributedList : ILifeCycle
    {
        private ReusableIndex _reusableIndex;
        private SecureStore<Jobs> _secureStorage;
        private CancellationTokenSource _cancellationTokenSource;
        private ILogger _logger;
        private Group _group;
        public Action<Job, IJobResult> OnJobCompleted;

        public List<Job> Values => _secureStorage.Value.List;

        public DistributedList() : this (DependencyInjection.DependencyManager.Get<IStore>(),
                                        DependencyInjection.DependencyManager.Get<ILogger>(),
                                        DependencyInjection.DependencyManager.Get<IGroupViewManager>()) { }
        public DistributedList(IStore store, ILogger logger, IGroupViewManager groupView)
        {
            _secureStorage = new SecureStore<Jobs>(store, logger);
            _reusableIndex = new ReusableIndex();
            _logger = logger;
            _group = groupView.View;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void DeletePendingAndRemovedJobs()
        {
            _secureStorage.Value.List.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
            _secureStorage.ValuesChanged.Invoke();
            _logger.Log(Tag.DistributedStorage, "Memory cleaned (logical deletions)");
        }

        public void AddOrUpdate(Job job)
        {        
            if (_secureStorage.Value.List.Contains(job))
                _secureStorage.Value.List.Remove(job);

            _secureStorage.Value.List.Add(job);
            _secureStorage.ValuesChanged.Invoke();
            
            _logger.Log(Tag.DistributedStorage, $"Job {job} added/updated");
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (_secureStorage.Value.List.Contains(job))
            {
                job.Status = JobStatus.REMOVED;
                _secureStorage.ValuesChanged.Invoke();
                _logger.Log(Tag.DistributedStorage, $"Job {job} logically removed");
            }
        }

        public void AddAndAssign(Job job)
        {
            job.Node = FindNodeWithLessJobs();
            job.ID = _reusableIndex.NewIndex;

            _secureStorage.Value.List.Add(job);
            _secureStorage.ValuesChanged.Invoke();

            if (job.Node.HasValue)
                _logger.Log(Tag.DistributedStorage, $"Job {job} assigned to {job.Node.Value}");
        }

        private int FindNodeWithLessJobs()
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

            _logger.Log(Tag.DistributedStorage, $"Total job assigned per node: {nodeJobCount.ToString()}");

            // Find the node with the less number of assignment
            (int, int) min = (_group.Me.ID.Value, nodeJobCount[_group.Me.ID.Value]);
            nodeJobCount.ForEach(nodeOccurencesPair => 
            {
                if (nodeOccurencesPair.Value < min.Item2)
                    min = (nodeOccurencesPair.Key, nodeOccurencesPair.Value);
            });

            _logger.Log(Tag.DistributedStorage, $"Node with less occurrences: {min.Item1} with {min.Item2} jobs to do");
            return min.Item1;
        }

        public async Task RunAssignedJob()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                Job current = FindJobToExecute(_group.Me);
                if (current != null)
                {
                    current.Status = JobStatus.RUNNING;
                    _secureStorage.ValuesChanged.Invoke();
                    _logger.Log(Tag.DistributedStorage, $"Job {current} RUNNING");

                    IJobResult result = await RunJob(current);
                    if (result == null) return;
                    
                    current.Status = JobStatus.COMPLETED;
                    _secureStorage.ValuesChanged.Invoke();
                    _logger.Log(Tag.DistributedStorage, $"Job {current} COMPLETED");
                    OnJobCompleted?.Invoke(current, result);
                }
            }
        }

        private async Task<IJobResult> RunJob(Job job)
        {
            try
            {
                return await job.Run();
            }
            catch when (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                return null;
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

        public void Init()
        {
            _secureStorage.Init();
            DeletePendingAndRemovedJobs();
        }

        public async void Start() => await RunAssignedJob();

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _secureStorage.Stop();
        } 
    }
}