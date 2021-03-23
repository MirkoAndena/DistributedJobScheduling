using System;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.JobAssignment
{
    public class JobExecutor : IStartable
    {
        private CancellationTokenSource _cancellationTokenSource;
        private ILogger _logger;
        private JobManager _storage;
        public Action<Job, IJobResult> OnJobCompleted;

        public JobExecutor(JobManager storage) : this (storage, DependencyInjection.DependencyManager.Get<ILogger>()) {}
        public JobExecutor(JobManager storage, ILogger logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public void Stop() => _cancellationTokenSource?.Cancel();
        
        public async void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                Job current = _storage.FindJobToExecute();
                if (current != null)
                {
                    _logger.Log(Tag.JobExecutor, $"Start to execute job {current}");
                    UpdateStatus(current, JobStatus.RUNNING);
                    
                    IJobResult result = await RunJob(current);
                    if (result == null) return;
                    
                    _logger.Log(Tag.JobExecutor, $"Job {current} has been executed");
                    UpdateStatus(current, JobStatus.COMPLETED);
                    OnJobCompleted?.Invoke(current, result);
                }
                else
                    return;
            }
        }

        private void UpdateStatus(Job job, JobStatus status)
        {
            job.Status = status;
            _storage.UpdateJob?.Invoke(job);
            _logger.Log(Tag.JobExecutor, $"Job {job} RUNNING");
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
    }
}