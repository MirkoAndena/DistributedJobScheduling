using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Tests.Communication;
using DistributedJobScheduling.Tests.Communication.Messaging;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit.Abstractions;
using System.Linq;

namespace DistributedJobScheduling.Tests.Utils
{
    public class EmptyMessage : Message 
    {
        public EmptyMessage() : base() {}
    }

    public class IdMessage : Message 
    {
        public int Id { get; private set; }
        public IdMessage(int id) : base() { Id = id; }
    }

    public class FakeNode
    {
        public Node Node { get; private set; }
        public ICommunicationManager Communication { get; private set; }
        public IGroupViewManager Group { get; private set; }
        public ITimeStamper TimeStamper { get; private set; }
        public Node.INodeRegistry Registry { get; private set; }
        public StubLogger Logger { get; private set; }
        public IConfigurationService Configuration { get; private set; }

        public FakeNode(int id, bool coordinator, StubNetworkBus networkBus, ITestOutputHelper logger, int joinTimeout = 5000)
        {
            Registry = new Node.NodeRegistryService();
            Node = Registry.GetOrCreate($"127.0.0.{id}", id);
            Logger = new StubLogger(Node, logger);
            Communication = new StubNetworkManager(Node, Logger);
            TimeStamper = new StubScalarTimeStamper(Node);
            Configuration = new FakeConfigurator(new Dictionary<string, object> {
                                                                    ["nodeId"] = id,
                                                                    ["coordinator"] = coordinator
                                                                  });
            Group = new GroupViewManager(Registry,
                                        Communication, 
                                        TimeStamper, 
                                        Configuration,
                                        Logger,
                                        joinTimeout);
            Group.View.ViewChanged += () => { Logger.Log(Logging.Tag.VirtualSynchrony, $"View Changed: {Group.View.Others.ToString<Node>()}"); };
            networkBus.RegisterToNetwork(Node, Registry, (StubNetworkManager)Communication);
        }

        public virtual void Start()
        {
            StartIfLifeCycle(Configuration);
            StartIfLifeCycle(Logger);
            StartIfLifeCycle(Registry);
            StartIfLifeCycle(TimeStamper);
            StartIfLifeCycle(Communication);
            StartIfLifeCycle(Group);
        }

        protected void StartIfLifeCycle(object Component)
        {
            if(Component is ILifeCycle lifeCycle)
                lifeCycle.Start();
        }
    }

    public static class NodeToolkit
    {
        public static void CreateView(IEnumerable<FakeNode> viewNodes, FakeNode coordinator)
        {
            viewNodes.ForEach(fakeNode => {
                HashSet<Node> nodeView = new HashSet<Node>();
                Node nodeCoordinator = null;
                viewNodes.Where(otherNode => otherNode != fakeNode).ForEach(otherNode => {
                    Node node = fakeNode.Registry.GetOrCreate(otherNode.Node);
                    nodeView.Add(node);

                    if(otherNode == coordinator)
                        nodeCoordinator = node;
                });

                if(fakeNode == coordinator)
                    nodeCoordinator = fakeNode.Node;

                fakeNode.Group.View.Update(nodeView, nodeCoordinator);
            });
        }

        public static async Task StartSequence(FakeNode[] nodes, int msBetweenNodes)
        {
            for(int i = 0; i < nodes.Length; i++)
            {
                nodes[i].Start();
                await Task.Delay(msBetweenNodes);
            }
        }
    }
}