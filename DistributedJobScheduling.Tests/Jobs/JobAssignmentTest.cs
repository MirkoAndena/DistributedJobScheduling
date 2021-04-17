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

namespace DistributedJobScheduling.Tests
{
    using JobCollection = Dictionary<int, Job>;
    public class JobAssignmentTest : JobUtils
    {
        private Group _group;
        private ILogger _logger;
        private BlockingDictionarySecureStore<JobCollection, int, Job> _secureStore;
        private ReusableIndex _index;

        public JobAssignmentTest(ITestOutputHelper testOutputHelper)
        {
            _group = TestElementsFactory.CreateStubGroup();
            _logger = new StubLogger(_group.Me, testOutputHelper);
            _secureStore = new BlockingDictionarySecureStore<JobCollection, int, Job>(new MemoryStore<JobCollection>(), _logger);
            _index = new ReusableIndex();
        }

        [Fact]
        public void NodeWithLessJobs()
        {
            StoreJobs();
            int id = JobUtils.FindNodeWithLessJobs(_group, _logger, _secureStore);
            Assert.Equal(2, id);
        }

        [Fact]
        public void OccurrencesTest()
        {
            StoreJobs();
            Dictionary<int, int> occurreces = JobUtils.FindNodesOccurrences(_group, _logger, _secureStore);
            Assert.Equal(4, occurreces[_group.Me.ID.Value]);
            Assert.Equal(3, occurreces[_group.Coordinator.ID.Value]);
            _group.Others.ForEach(node => Assert.Equal(node.ID.Value, occurreces[node.ID.Value]));
        }
        
        private void StoreJobs()
        {
            // Me => 2
            // Coordinator => 3
            // Others => ID number

            _secureStore.Init();

            StoreJob(_group.Me);
            StoreJob(_group.Me);
            StoreJob(_group.Me);
            StoreJob(_group.Me);
            
            StoreJob(_group.Coordinator);
            StoreJob(_group.Coordinator);
            StoreJob(_group.Coordinator);

            _group.Others.ForEach(node => Enumerable.Range(0, node.ID.Value).ForEach(i => StoreJob(node)));
        }

        private void StoreJob(Node owner)
        {
            Job job = new TimeoutJob(1);
            job.ID = _index.NewIndex;
            job.Node = owner.ID.Value;
            _secureStore.Add(job.ID.Value, job);
        }
    }
}