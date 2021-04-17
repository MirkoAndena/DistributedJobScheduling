using System;
using System.Threading.Tasks;
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.Tests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace DistributedJobScheduling.Tests
{
    public class JobExecutorTest
    {
        private JobExecutor _executor;
        private JobStorage _store;

        public JobExecutorTest(ITestOutputHelper output)
        {
            (JobStorage, JobExecutor) _ = TestElementsFactory.CreateJobManagerAndExecutor(output);
            _store = _.Item1;
            _executor = _.Item2;
            _store.Init();
        }

        [Fact]
        public async void JobRun()
        {
            bool executed = false;
            Random random = new Random();
            _store.InsertAndAssign(new TimeoutJob(2 + random.Next(7)));
            _store.InsertAndAssign(new TimeoutJob(2 + random.Next(7)));
            _store.InsertAndAssign(new TimeoutJob(2 + random.Next(7)));
            _store.InsertAndAssign(new TimeoutJob(2 + random.Next(7)));
            _store.JobUpdated += job =>
            {
                executed = job.Status == JobStatus.COMPLETED;
            };
            _executor.Start();
            await Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(t => 
            {
                _executor.Stop();
                Assert.True(executed);
            });
        }
    }
}