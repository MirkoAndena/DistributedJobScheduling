using System.Linq;
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
using DistributedJobScheduling.Queues;

namespace DistributedJobScheduling.Storage
{
    public class JobStorage : IJobStorage, IInitializable, IViewStatefull
    {
        private ReusableIndex _reusableIndex;
        private AsyncGenericQueue<int> _executionBlips;
        protected BlockingDictionarySecureStore<Dictionary<int, Job>, int, Job> _secureStore;
        private ILogger _logger;
        private Group _group;
        private HashSet<Job> _executionSet;
        public event Action<Job> JobUpdated;

        public JobStorage() : this (
            DependencyInjection.DependencyManager.Get<IStore<Dictionary<int, Job>>>(), 
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<IGroupViewManager>()) { }
        
        public JobStorage(IStore<Dictionary<int, Job>> store, ILogger logger, IGroupViewManager groupView)
        {
            _secureStore = new BlockingDictionarySecureStore<Dictionary<int, Job>, int, Job>(store, logger);
            _reusableIndex = new ReusableIndex(index => _secureStore.ContainsKey(index));
            _executionBlips = new AsyncGenericQueue<int>();
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
            _secureStore.RemoveAll(job => 
                job.Status == JobStatus.PENDING || 
                job.Status == JobStatus.REMOVED);
            _secureStore.ValuesChanged.Invoke();
            _logger.Log(Tag.JobStorage, "Memory cleaned (logical deletions)");
        }

        public void UpdateJob(Job job)
        {   
            if (job.ID.HasValue && _secureStore.ContainsKey(job.ID.Value))
            {
                if(job.Status != JobStatus.RUNNING && _executionSet.Contains(job))
                    _executionSet.Remove(job);
                _secureStore.ValuesChanged?.Invoke();
                JobUpdated?.Invoke(job);
            }
        }

        public void SetJobDeliveredToClient(Job job)
        {
            if (job.ID.HasValue && _secureStore.ContainsKey(job.ID.Value))
            {
                job.Status = JobStatus.REMOVED;
                _secureStore.ValuesChanged?.Invoke();
                JobUpdated?.Invoke(job);
                _logger.Log(Tag.JobStorage, $"Job {job} logically removed");
            }
        }

        public Job Get(int jobID) => _secureStore[jobID];

        public void InsertAndAssign(Job job)
        {
            lock(_secureStore)
            {
                job.Node = JobUtils.FindNodeWithLessJobs(_group, _logger, _secureStore);
                job.ID = _reusableIndex.NewIndex;
                _logger.Log(Tag.JobStorage, $"new job id: {job.ID}");

                _secureStore.Add(job.ID.Value, job);
                _secureStore.ValuesChanged?.Invoke();
                JobUpdated?.Invoke(job);

                _logger.Log(Tag.JobStorage, $"Job {job} assigned to {job.Node.Value}");
            }
            UnlockJobExecution();
        }

        public void InsertOrUpdateJobLocally(Job job)
        {
            // If the job is already in the list it is updated
            if (_secureStore.ContainsKey(job.ID.Value))
            {
                Job localJob = _secureStore[job.ID.Value];
                localJob.Node = job.Node;
                if(localJob.Node != _group.Me.ID)
                    localJob.Result = job.Result;
                if(localJob.Node != _group.Me.ID && localJob.Status < job.Status)
                    localJob.Status = job.Status;
            }
            else
            {
                _secureStore.Add(job.ID.Value, job);
            }
            _secureStore.ValuesChanged?.Invoke();
            _logger.Log(Tag.JobStorage, $"Job {job} inserted locally{(job.Result == null ? "" : ", result: " + job.Result.ToString())}");
            UnlockJobExecution();
        }

        private void UnlockJobExecution()
        {
            _executionBlips.Enqueue(0);
        }

        public async Task<Job> FindJobToExecute(CancellationToken token)
        {
            _logger.Log(Tag.JobStorage, "Finding job to execute");
            await _executionBlips.Dequeue(token);
            Job toExecute = null;
            _secureStore.ExecuteTransaction(storedJobs =>
                storedJobs.Values.ForEach(job => 
                {
                    if (job.Node == _group.Me.ID && (job.Status == JobStatus.PENDING || (job.Status == JobStatus.RUNNING && !_executionSet.Contains(job))))
                    {
                        _logger.Log(Tag.JobStorage, $"Found job {job}");
                        toExecute = job;
                        _executionSet.Add(toExecute);
                    }
                })
            );

            return toExecute;
        }

        public Message ToSyncMessage()
        {
            return new JobSyncMessage(_secureStore.Values.ToList());
        }

        public void OnViewSync(Message syncMessage)
        {
            _logger.Log(Tag.JobStorage, "Synching jobs with coordinator!");
            if(syncMessage is JobSyncMessage jobSyncMessage)
            {
                _secureStore.ExecuteTransaction(jobs => {
                    jobs.Clear();
                    jobSyncMessage.Jobs.ForEach(job => jobs.Add(job.ID.Value, job));
                    _secureStore.ValuesChanged?.Invoke();
                });
                _logger.Log(Tag.JobStorage, "Job synchronization complete");
            }
            UnlockJobExecution();
        }
    }
}