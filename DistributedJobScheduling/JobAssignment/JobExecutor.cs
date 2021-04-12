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
        private JobStorage _jobStorage;

        public JobExecutor(JobStorage jobStorage) : this (
            jobStorage,
            DependencyInjection.DependencyManager.Get<ILogger>()) {}

        public JobExecutor(JobStorage jobStorage, ILogger logger)
        {
            _jobStorage = jobStorage;
            _logger = logger;
        }

        public void Stop() => _cancellationTokenSource?.Cancel();

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            Action findAndExecute = async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Job current = await _jobStorage.FindJobToExecute();
                    if (current != null)
                        await ExecuteJob(current);
                }
            };

            _ = Task.Run(findAndExecute, _cancellationTokenSource.Token);
        }

        private async Task ExecuteJob(Job current)
        {
            _logger.Log(Tag.JobExecutor, $"Start to execute job {current}");
            UpdateStatus(current, JobStatus.RUNNING);
            
            IJobResult result = await RunJob(current);
            if (result == null) return;
            else current.Result = result;
            
            _logger.Log(Tag.JobExecutor, $"Job {current} has been executed");
            UpdateStatus(current, JobStatus.COMPLETED);
        }

        private void UpdateStatus(Job job, JobStatus status)
        {
            job.Status = status;
            _jobStorage.UpdateJob(job);
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