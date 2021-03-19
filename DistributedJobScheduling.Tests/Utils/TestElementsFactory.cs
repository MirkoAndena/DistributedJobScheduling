using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Storage;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit.Abstractions;

namespace DistributedJobScheduling.Tests.Utils
{
    public static class TestElementsFactory
    {
        public static JobStorage CreateJobStorage(ITestOutputHelper output)
        {
            Group group = CreateStubGroup();
            StubLogger stubLogger = new StubLogger(group.Me, output);
            return new JobStorage(new MemoryStore<Jobs>(), stubLogger, new FakeGroupViewManager(group));
        }

        private static Group CreateStubGroup()
        {
            Node.INodeRegistry registry = new Node.NodeRegistryService();
            Group group = new Group(registry.GetOrCreate("127.0.0.1", 0));
            group.UpdateCoordinator(registry.GetOrCreate("127.0.0.2", 1));
            group.Add(registry.GetOrCreate("127.0.0.3", 2));
            group.Add(registry.GetOrCreate("127.0.0.4", 3));
            return group;
        }
    }
}