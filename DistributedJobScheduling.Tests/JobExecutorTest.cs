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
            _store = TestElementsFactory.CreateJobStorage(output);
            _executor = new JobExecutor(_store);
        }

        [Fact]
        public void JobRun()
        {
            _store.InsertAndAssign(new TimeoutJob(1));
            _store.InsertAndAssign(new TimeoutJob(1));
            _store.InsertAndAssign(new TimeoutJob(1));
            _store.InsertAndAssign(new TimeoutJob(1));
            _executor.OnJobCompleted += (job, result) => 
            {
                Assert.True(((BooleanJobResult)result).Value);
            };
            _executor.RunAssignedJob();
            //Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(t => _store.Stop());
        }
    }
}