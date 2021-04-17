using System;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Storage.SecureStorage;
using DistributedJobScheduling.Tests.Utils;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit.Abstractions;
using Xunit;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Storage;

namespace DistributedJobScheduling.Tests.Jobs
{
    using Store = BlockingDictionarySecureStore<Dictionary<int, Job>, int, Job>;

    public class JobExecutorTest
    {
        private Group _group;
        private ILogger _logger;
        private Store _secureStore;
        private ReusableIndex _index;
        private IJobStorage _jobStorage;
        private JobExecutor _executor;

        public JobExecutorTest(ITestOutputHelper testOutputHelper) : base()
        {
            _group = TestElementsFactory.CreateStubGroup();
            _logger = new StubLogger(_group.Me, testOutputHelper);
            IStore<Dictionary<int, Job>> store = new MemoryStore<Dictionary<int, Job>>();
            _secureStore = new Store(store, _logger);
            _index = new ReusableIndex();
            _jobStorage = new JobStorage(store, _logger, new FakeGroupViewManager(_group));
            _executor = new JobExecutor(_jobStorage, _logger);
            _secureStore.Init();
        }

        [Fact]
        public async void ExecuteMyJobsTest()
        {
            // 4 jobs assigned to me
            JobTestUtils.StoreJobs(_index, _group, _secureStore);
            bool executedAll = false;
            _executor.Start();

            await Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(t => 
            {
                int count = _secureStore.Count(value => value.Node.Value == _group.Me.ID.Value && value.Status == JobStatus.COMPLETED);
                Assert.Equal(4, count);
                executedAll = true;
            });

            Assert.True(executedAll);
        }
    }
}