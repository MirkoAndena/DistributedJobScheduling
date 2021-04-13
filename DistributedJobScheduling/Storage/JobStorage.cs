using System.IO.Compression;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging.JobAssignment;

namespace DistributedJobScheduling.Storage
{
    public class JobCollection
    { 
        public List<Job> List;

        public JobCollection() { List = new List<Job>(); }
    }

    public class JobStorage : IJobStorage, IInitializable, IViewStatefull
    {
        private ReusableIndex _reusableIndex;
        private SemaphoreSlim _checkJobSemaphore;
        private SecureStore<JobCollection> _secureStore;
        private ILogger _logger;
        private Group _group;
        private HashSet<Job> _executionSet;
        public event Action<Job> JobUpdated;

        public JobStorage() : this (
            DependencyInjection.DependencyManager.Get<IStore<JobCollection>>(), 
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<IGroupViewManager>()) { }
        
        public JobStorage(IStore<JobCollection> store, ILogger logger, IGroupViewManager groupView)
        {
            _secureStore = new SecureStore<JobCollection>(store, logger);
            _reusableIndex = new ReusableIndex();
            _checkJobSemaphore = new SemaphoreSlim(1,1);
            _logger = logger;
            _group = groupView.View;
        }

        public void Init()
        {
            _secureStore.Init();
            _executionSet = new HashSet<Job>();
            DeletePendingAndRemovedJobs();
        }

        private void DeletePendingAndRemovedJobs()
        {
            _secureStore.Value.List.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
            _secureStore.ValuesChanged.Invoke();
            _logger.Log(Tag.JobStorage, "Memory cleaned (logical deletions)");
        }

        public void UpdateJob(Job job)
        {   
            if (_secureStore.Value.List.Contains(job))
            {
                if(job.Status != JobStatus.RUNNING && _executionSet.Contains(job))
                    _executionSet.Remove(job);
                _secureStore.ValuesChanged?.Invoke();
                JobUpdated?.Invoke(job);
            }
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (_secureStore.Value.List.Contains(job))
            {
                job.Status = JobStatus.REMOVED;
                _secureStore.ValuesChanged?.Invoke();
                JobUpdated?.Invoke(job);
                _logger.Log(Tag.JobStorage, $"Job {job} logically removed");
            }
        }

        public Job Get(int jobID) 
        {
            foreach (Job job in _secureStore.Value.List)
            {
                if (job.ID.HasValue && job.ID.Value == jobID)
                    return job;
            }
            return null;
        }

        public void InsertAndAssign(Job job)
        {
            job.Node = JobUtils.FindNodeWithLessJobs(_group, _logger, _secureStore);
            job.ID = _reusableIndex.NewIndex;

            _secureStore.Value.List.Add(job);
            _secureStore.ValuesChanged?.Invoke();
            JobUpdated?.Invoke(job);

            _logger.Log(Tag.JobStorage, $"Job {job} assigned to {job.Node.Value}");
            UnlockJobExecution();
        }

        public void InsertOrUpdateJobLocally(Job job)
        {
            // If the job is already in the list it is updated
            if (_secureStore.Value.List.Contains(job))
            {
                _logger.Warning(Tag.JobStorage, $"Job {job} is already in the storage, update refused");
                return;
            }

            // Remove job with same ID
            _secureStore.Value.List.RemoveAll(current => current.ID.HasValue && current.ID.Value == job.ID.Value);

            _secureStore.Value.List.Add(job);
            _secureStore.ValuesChanged?.Invoke();
            _logger.Log(Tag.JobStorage, $"Job {job} inserted locally");
            UnlockJobExecution();
        }

        private void UnlockJobExecution()
        {
            lock(_checkJobSemaphore)
            {
                if(_checkJobSemaphore.CurrentCount != 1) _checkJobSemaphore.Release();
            }
        }

        public async Task<Job> FindJobToExecute()
        {
            await _checkJobSemaphore.WaitAsync();
            Job toExecute = null;
            _secureStore.Value.List.ForEach(job => 
            {
                if (job.Node == _group.Me.ID && (job.Status == JobStatus.PENDING || (job.Status == JobStatus.RUNNING && !_executionSet.Contains(job))))
                {
                    toExecute = job;
                    _executionSet.Add(toExecute);
                }
            });
            return toExecute;
        }

        public Message ToSyncMessage()
        {
            return new JobSyncMessage(_secureStore.Value.List);
        }

        public void OnViewSync(Message syncMessage)
        {
            _logger.Log(Tag.JobStorage, "Synching jobs with coordinator!");
            if(syncMessage is JobSyncMessage jobSyncMessage)
            {
                _secureStore.Value.List.Clear();
                _secureStore.Value.List.AddRange(jobSyncMessage.Jobs);
                _secureStore.ValuesChanged?.Invoke();
                _logger.Log(Tag.JobStorage, "Job synchronization complete");
            }
            UnlockJobExecution();
        }
    }
}