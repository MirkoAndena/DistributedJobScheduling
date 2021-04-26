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
        public event Action<Job> JobCreated;
        private Dictionary<Job, TaskCompletionSource<bool>> _unCommitted;

        public JobStorage() : this (
            DependencyInjection.DependencyManager.Get<IStore<Dictionary<int, Job>>>(), 
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<IGroupViewManager>()) { }
        
        public JobStorage(IStore<Dictionary<int, Job>> store, ILogger logger, IGroupViewManager groupView)
        {
            _secureStore = new BlockingDictionarySecureStore<Dictionary<int, Job>, int, Job>(store, logger);
            _reusableIndex = new ReusableIndex(_secureStore);
            _executionBlips = new AsyncGenericQueue<int>();
            _logger = logger;
            _group = groupView.View;
            _unCommitted = new Dictionary<Job, TaskCompletionSource<bool>>();
        }

        public void Init()
        {
            _secureStore.Init();
            _executionSet = new HashSet<Job>();
            DeleteRemovedJobs();
        }

        private void DeleteRemovedJobs()
        {
            _secureStore.RemoveAll(job => job.Status == JobStatus.REMOVED);
            _secureStore.ValuesChanged.Invoke();
            _logger.Log(Tag.JobStorage, "Memory cleaned (logical deletions)");
        }

        public async Task UpdateStatus(int id, JobStatus status) => await UpdateJob(id, job => job.Status = status, job => job.Status < status);

        public async Task UpdateResult(int id, IJobResult result) => await UpdateJob(id, job => { job.Result = result; job.Status = JobStatus.COMPLETED; });

        public async Task UpdateJob(int id, Action<Job> update, Predicate<Job> updateCondition = null)
        {   
            if (_secureStore.ContainsKey(id))
            {
                if (updateCondition == null || updateCondition.Invoke(_secureStore[id]))
                {
                    _logger.Log(Tag.JobStorage, $"Updating {_secureStore[id]} and wait for commit");
                    Job clone = _secureStore[id].Clone();
                    update.Invoke(clone);
                    
                    var taskCompletionSource = new TaskCompletionSource<bool>(TaskContinuationOptions.RunContinuationsAsynchronously);
                    _unCommitted.Add(_secureStore[id], taskCompletionSource);
                    
                    JobUpdated?.Invoke(clone);                    
                    await taskCompletionSource.Task;
                }
            }
        }

        public Job Get(int jobID) => _secureStore[jobID];

        public int CreateJob(IJobWork jobWork)
        {
            if (!_group.ImCoordinator)
            {
                _logger.Fatal(Tag.JobStorage, "Job creation unpermitted", new Exception("I'm not the leader, i can't create jobs"));
            }

            int id;
            lock(_secureStore)
            {
                id = _reusableIndex.NewIndex;
                int assignedNode = JobUtils.FindNodeWithLessJobs(_group, _logger, _secureStore);
                _logger.Log(Tag.JobStorage, $"new job id: {id}");

                Job job = new Job(id, assignedNode, jobWork);
                _secureStore.Add(id, job);
                _secureStore.ValuesChanged?.Invoke();
                JobCreated?.Invoke(job);
                
                _logger.Log(Tag.JobStorage, $"Job {job} assigned to {assignedNode}");
            }

            UnlockJobExecution();
            return id;
        }

        public void CommitUpdate(Job updated)
        {
            // If the job is already in the list it is updated
            if (_secureStore.ContainsKey(updated.ID))
            {
                Job job = _secureStore[updated.ID];
                if (updated.Status >= _secureStore[updated.ID].Status)
                {
                    job.Update(updated);
                    _logger.Log(Tag.JobStorage, $"Job {job.ToString()} updated locally");
                }
                else
                    _logger.Log(Tag.JobStorage, $"Job {job.ToString()} was not updated because it has a greater status");

                if (_unCommitted.ContainsKey(job))
                {
                    var source = _unCommitted[job];
                    _unCommitted.Remove(job);
                    source.SetResult(true);
                    _logger.Log(Tag.JobStorage, $"Update committed for {job.ToString()}, lock released");
                }
            }
            else
            {
                _secureStore.Add(updated.ID, updated);
                _logger.Log(Tag.JobStorage, $"Job {updated.ToString()} inserted locally");
            }

            _secureStore.ValuesChanged?.Invoke();
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
                    jobSyncMessage.Jobs.ForEach(job => jobs.Add(job.ID, job));
                    _secureStore.ValuesChanged?.Invoke();
                });
                _logger.Log(Tag.JobStorage, "Job synchronization complete");
            }
            UnlockJobExecution();
        }
    }
}