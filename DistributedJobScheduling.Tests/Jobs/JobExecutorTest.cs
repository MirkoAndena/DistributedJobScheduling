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
using DistributedJobScheduling.LifeCycle;

namespace DistributedJobScheduling.Tests.Jobs
{
    using Store = BlockingDictionarySecureStore<Dictionary<int, Job>, int, Job>;

    class TestJobStorage : JobStorage
    {
        public TestJobStorage( IStore<Dictionary<int, Job>> store, ILogger logger, IGroupViewManager groupViewManager) : 
        base (store, logger, groupViewManager) { }

        public Store Store => _secureStore;
    }

    public class JobExecutorTest
    {
        private Group _group;
        private ILogger _logger;
        private ReusableIndex _index;
        private TestJobStorage _jobStorage;
        private JobExecutor _executor;

        public JobExecutorTest(ITestOutputHelper testOutputHelper) : base()
        {
            _group = TestElementsFactory.CreateStubGroup();
            _logger = new StubLogger(_group.Me, testOutputHelper);
            IStore<Dictionary<int, Job>> store = new MemoryStore<Dictionary<int, Job>>();
            _index = new ReusableIndex();
            _jobStorage = new TestJobStorage(store, _logger, new FakeGroupViewManager(_group));
            _executor = new JobExecutor(_jobStorage, _logger);
            _jobStorage.Init();
        }

        [Fact]
        public async void ExecuteMyJobsTest()
        {
            _jobStorage.CommitUpdate(new Job(_index.NewIndex, _group.Me.ID.Value, new TimeoutJobWork(1)));
            _jobStorage.CommitUpdate(new Job(_index.NewIndex, _group.Me.ID.Value, new TimeoutJobWork(1)));
            _jobStorage.CommitUpdate(new Job(_index.NewIndex, _group.Me.ID.Value, new TimeoutJobWork(1)));

            bool executedAll = false;
            _executor.Start();

            await Task.Delay(TimeSpan.FromSeconds(20)).ContinueWith(t => 
            {
                int count = _jobStorage.Store.Count(value => value.Node == _group.Me.ID.Value && value.Status == JobStatus.COMPLETED);
                Assert.Equal(3, count);
                executedAll = true;
            });

            Assert.True(executedAll);
        }
    }
}