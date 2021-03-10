using Xunit;
using DistributedJobScheduling.DistributedStorage;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.Communication.Basic;
using System.Collections.Generic;

namespace DistributedJobScheduling.Tests
{
    public class DistributedStorageTest
    {
        private Group CreateStubGroup()
        {
            // Group with 4 nodes
            Group group = new Group(0);
            group.AddCoordinator(new Node("127.0.0.1", 1));
            group.Add(new Node("127.0.0.1", 2));
            group.Add(new Node("127.0.0.1", 3));
            return group;
        }

        private void CreateJobAndAssign(DistributedList storage, Group group)
        {
            TimeoutJob job = new TimeoutJob(0);
            storage.AddAndAssign(job, group);
        }

        [Fact]
        public void JobAssignment()
        {
            DistributedList storage = new DistributedList(new SecureStorageStub());
            Group group = CreateStubGroup();

            CreateJobAndAssign(storage, group);
            CreateJobAndAssign(storage, group);
            CreateJobAndAssign(storage, group);
            Assert.True(MaxJobPerNode(storage) == 1);

            CreateJobAndAssign(storage, group);
            Assert.True(MaxJobPerNode(storage) == 1);
            
            CreateJobAndAssign(storage, group);
            Assert.True(MaxJobPerNode(storage) == 2);
        }

        private int MaxJobPerNode(DistributedList list)
        {
            int max = 0;

            Dictionary<int, int> occurences = new Dictionary<int, int>();
            foreach (Job job in list.Values)
                if (occurences.ContainsKey(job.Node))
                    occurences[job.Node]++;
                else
                    occurences.Add(job.Node, 1);

            foreach (int o in occurences.Values)
                if (o > max) max = o;

            return max;
        }
    }
}