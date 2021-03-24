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
        private JobManager _store;

        public JobExecutorTest(ITestOutputHelper output)
        {
            (JobManager, JobExecutor) _ = TestElementsFactory.CreateJobManagerAndExecutor(output);
            _store = _.Item1;
            _executor = _.Item2;
            _store.Init();
        }

        [Fact]
        public async void JobRun()
        {
            bool executed = false;
            _store.InsertAndAssign(new TimeoutJob(1));
            _store.InsertAndAssign(new TimeoutJob(1));
            _store.InsertAndAssign(new TimeoutJob(1));
            _store.InsertAndAssign(new TimeoutJob(1));
            _executor.OnJobCompleted += (job, result) => 
            {
                Assert.True(((BooleanJobResult)result).Value);
                executed = true;
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