using System.Threading.Tasks;
using System;
using Xunit;
using DistributedJobScheduling.DistributedStorage;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.Communication.Basic;
using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Xunit.Abstractions;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.Tests.Utils;

namespace DistributedJobScheduling.Tests
{
    public class JobManagerTest
    {
        private JobManager _store;

        public JobManagerTest(ITestOutputHelper output)
        {
            _store = TestElementsFactory.CreateJobManager(output);
            _store.Init();
        }

        [Fact]
        public void JobAssignment()
        {
            Assert.True(MaxJobPerNode(_store) == 0);
            
            _store.InsertAndAssign(new TimeoutJob(0));
            _store.InsertAndAssign(new TimeoutJob(0));
            _store.InsertAndAssign(new TimeoutJob(0));
            Assert.True(MaxJobPerNode(_store) == 1);

            _store.InsertAndAssign(new TimeoutJob(0));
            Assert.True(MaxJobPerNode(_store) == 1);
            
            _store.InsertAndAssign(new TimeoutJob(0));
            Assert.True(MaxJobPerNode(_store) == 2);
        }

        private int MaxJobPerNode(JobManager list)
        {
            int max = 0;
            Dictionary<int, int> occurences = _store.FindNodesOccurrences();
            foreach (int o in occurences.Values)
                if (o > max) max = o;
            return max;
        }
    }
}