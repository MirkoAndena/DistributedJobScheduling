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
        private IJobStorage _jobStorage;

        public JobExecutor() : this (
            DependencyInjection.DependencyManager.Get<IJobStorage>(),
            DependencyInjection.DependencyManager.Get<ILogger>()) {}

        public JobExecutor(IJobStorage jobStorage, ILogger logger)
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
                    try
                    {
                        Job current = await _jobStorage.FindJobToExecute(_cancellationTokenSource.Token);
                        if (current != null)
                            await ExecuteJob(current);
                    }
                    catch(OperationCanceledException)
                    {
                        _logger.Warning(Tag.JobExecutor, "FindJobToExecute cancelled");
                    }
                    catch(AggregateException ex)
                    {
                        _logger.Warning(Tag.JobExecutor, "FindJobToExecute aggregate exception", ex);
                    }
                }
            };

            Task.Run(findAndExecute, _cancellationTokenSource.Token);
        }

        private async Task ExecuteJob(Job current)
        {
            _logger.Log(Tag.JobExecutor, $"Start to execute job {current}");
            await _jobStorage.UpdateStatus(current.ID, JobStatus.RUNNING);
            
            IJobResult result = await RunJob(current);
            
            _logger.Log(Tag.JobExecutor, $"Job {current} has been executed");
            if (result != null)
            {
                await _jobStorage.UpdateResult(current.ID, result);
                _logger.Log(Tag.JobExecutor, $"Result {result.GetType().Name} of job {current.ID} updated");
            }
        }

        private async Task<IJobResult> RunJob(Job job)
        {
            _logger.Log(Tag.JobExecutor, $"Running job {job.ToString()}");
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