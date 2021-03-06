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

    public class JobAssignmentTest : JobUtils
    {
        private Group _group;
        private ILogger _logger;
        private Store _secureStore;
        private ReusableIndex _index;

        public JobAssignmentTest(ITestOutputHelper testOutputHelper)
        {
            _group = TestElementsFactory.CreateStubGroup();
            _logger = new StubLogger(_group.Me, testOutputHelper);
            _secureStore = new Store(new MemoryStore<Dictionary<int, Job>>(), _logger);
            _index = new ReusableIndex();
            _secureStore.Init();
        }

        [Fact]
        public void NodeWithLessJobs()
        {
            JobTestUtils.StoreJobs(_index, _group, _secureStore);
            int id = JobUtils.FindNodeWithLessJobs(_group, _logger, _secureStore);
            Assert.Equal(2, id);
        }

        [Fact]
        public void OccurrencesTest()
        {
            JobTestUtils.StoreJobs(_index, _group, _secureStore);
            Dictionary<int, int> occurreces = JobUtils.FindNodesOccurrences(_group, _logger, _secureStore);
            Assert.Equal(4, occurreces[_group.Me.ID.Value]);
            Assert.Equal(3, occurreces[_group.Coordinator.ID.Value]);
            _group.Others.ForEach(node => Assert.Equal(node.ID.Value, occurreces[node.ID.Value]));
        }
    }
}