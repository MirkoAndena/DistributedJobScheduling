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
    public class JobExecutor
    {
        private CancellationTokenSource _cancellationTokenSource;
        private ILogger _logger;
        private JobStorage _storage;
        public Action<Job, IJobResult> OnJobCompleted;

        public JobExecutor(JobStorage storage) : this (storage, DependencyInjection.DependencyManager.Get<ILogger>()) {}
        public JobExecutor(JobStorage storage, ILogger logger)
        {
            _storage = storage;
            _logger = logger;
        }
        
        public async Task RunAssignedJob()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                Job current = _storage.FindJobToExecute();
                if (current != null)
                {
                    UpdateStatus(current, JobStatus.RUNNING);
                    
                    IJobResult result = await RunJob(current);
                    if (result == null) return;
                    
                    UpdateStatus(current, JobStatus.COMPLETED);
                    OnJobCompleted?.Invoke(current, result);
                }
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