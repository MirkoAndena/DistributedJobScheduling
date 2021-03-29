using System;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;
using static DistributedJobScheduling.Communication.Basic.Node;

namespace DistributedJobScheduling.Client
{
    public class WorkerSearcher : IStartable
    {
        private Shouter _shouter;
        private ILogger _logger;
        public event Action<Node> WorkerFound;
        private bool _found;

        public WorkerSearcher(ISerializer serializer) : this (
            DependencyInjection.DependencyManager.Get<Node.INodeRegistry>(),
            DependencyInjection.DependencyManager.Get<ILogger>(),
            serializer) { }

        public WorkerSearcher(INodeRegistry nodeRegistry, ILogger logger, ISerializer serializer)
        {
            _logger = logger;
            _shouter = new Shouter(nodeRegistry, logger, serializer);
            _shouter.OnMessageReceived += OnWorkerAvailableResponseArrived;
            _found = false;
        }

        public void Start()
        {
            _logger.Log(Tag.WorkerSearcher, "Searching for an available worker");
            _shouter.Start();
            _shouter.SendMulticast(new WorkerAvaiableRequest()).Wait();
        }

        public void Stop()
        {
            _shouter.OnMessageReceived -= OnWorkerAvailableResponseArrived;
            _shouter.Stop();
        }

        private void OnWorkerAvailableResponseArrived(Node node, Message message)
        {
            if (!_found && message is WorkerAvaiableResponse)
            {
                _logger.Log(Tag.WorkerSearcher, $"Worker found: {node.ToString()}");
                WorkerFound?.Invoke(node);
                _found = true;
                this.Stop();
            }
        }
    }
}