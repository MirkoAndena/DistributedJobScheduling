using System.Threading.Tasks;
using System;
using Xunit;
using DistributedJobScheduling.DistributedStorage;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.Communication.Basic;
using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;
using Xunit.Abstractions;

namespace DistributedJobScheduling.Tests
{
    public class DistributedStorageTest
    {
        private DistributedList _list;

        public DistributedStorageTest(ITestOutputHelper output)
        {
            Group group = CreateStubGroup();
            StubLogger stubLogger = new StubLogger(group.Me, output);
            _list = new DistributedList(new SecureStorageStub(), stubLogger, new FakeGroupViewManager(group));
            _list.Init();
        }

        private Group CreateStubGroup()
        {
            Node.INodeRegistry registry = new Node.NodeRegistryService();
            Group group = new Group(registry.GetOrCreate("127.0.0.1", 0));
            group.UpdateCoordinator(registry.GetOrCreate("127.0.0.2", 1));
            group.Add(registry.GetOrCreate("127.0.0.3", 2));
            group.Add(registry.GetOrCreate("127.0.0.4", 3));
            return group;
        }

        [Fact]
        public void JobAssignment()
        {
            _list.AddAndAssign(new TimeoutJob(0));
            _list.AddAndAssign(new TimeoutJob(0));
            _list.AddAndAssign(new TimeoutJob(0));
            Assert.True(MaxJobPerNode(_list) == 1);

            _list.AddAndAssign(new TimeoutJob(0));
            Assert.True(MaxJobPerNode(_list) == 1);
            
            _list.AddAndAssign(new TimeoutJob(0));
            Assert.True(MaxJobPerNode(_list) == 2);
        }

        [Fact]
        public void JobRun()
        {
            _list.AddAndAssign(new TimeoutJob(1));
            _list.AddAndAssign(new TimeoutJob(1));
            _list.AddAndAssign(new TimeoutJob(1));
            _list.AddAndAssign(new TimeoutJob(1));
            _list.OnJobCompleted += (job, result) => 
            {
                Assert.True(((BooleanJobResult)result).Value);
            };
            _list.Start();
            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(t => _list.Stop());
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