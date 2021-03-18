using Xunit;
using DistributedJobScheduling.DistributedStorage;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.Communication.Basic;
using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Tests
{
    public class DistributedStorageTest
    {
        private Group CreateStubGroup()
        {
            Node.INodeRegistry registry = new Node.NodeRegistryService();
            // Group with 4 nodes
            Group group = new Group(registry.GetOrCreate("127.0.0.1", 0));
            group.UpdateCoordinator(registry.GetOrCreate("127.0.0.2", 1));
            group.Add(registry.GetOrCreate("127.0.0.3", 2));
            group.Add(registry.GetOrCreate("127.0.0.4", 3));
            return group;
        }

        private void CreateJobAndAssign(DistributedList storage, Group group)
        {
            TimeoutJob job = new TimeoutJob(0);
            //storage.AddAndAssign(job, group);
        }

        [Fact]
        public void JobAssignment()
        {
            DistributedList storage = new DistributedList(new SecureStorageStub(), new StubLogger(), null);
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
            {
                // Here should be always true
                if (job.Node.HasValue)
                {
                    if (occurences.ContainsKey(job.Node.Value))
                        occurences[job.Node.Value]++;
                    else
                        occurences.Add(job.Node.Value, 1);
                }
            }

            foreach (int o in occurences.Values)
                if (o > max) max = o;

            return max;
        }
    }
}