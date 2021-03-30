using System.Threading.Tasks;
using System.Threading;
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
        private ClientStore _store;
        public event Action<Node> WorkerFound;
        private bool _found;
        private CancellationTokenSource _cancelMulticast;

        public WorkerSearcher(ClientStore clientStore) : this (
            DependencyInjection.DependencyManager.Get<Node.INodeRegistry>(),
            DependencyInjection.DependencyManager.Get<ILogger>(),
            DependencyInjection.DependencyManager.Get<ISerializer>(),
            clientStore) { }

        public WorkerSearcher(INodeRegistry nodeRegistry, ILogger logger, ISerializer serializer, ClientStore clientStore)
        {
            _logger = logger;
            _store = clientStore;
            _shouter = new Shouter(nodeRegistry, logger, serializer);
            _shouter.OnMessageReceived += OnWorkerAvailableResponseArrived;
            _found = false;
        }

        public void Start()
        {
            _logger.Log(Tag.WorkerSearcher, "Searching for an available worker");
            _cancelMulticast = new CancellationTokenSource();
            
            if (_store.IsWorkerPresent)
            {
                Node worker = _store.GetWorker();
                _found = true;
                _logger.Log(Tag.WorkerSearcher, $"Worker {worker.ToString()} found into store");
                WorkerFound?.Invoke(worker);
            }
            else
            {
                _logger.Log(Tag.WorkerSearcher, $"No worker stored, started multicast search");
                _shouter.Start();
                _shouter.SendMulticast(new WorkerAvaiableRequest()).Wait();

                Task.Delay(TimeSpan.FromSeconds(10), _cancelMulticast.Token).
                ContinueWith(t => 
                { 
                    if (!t.IsCanceled)
                        _shouter.SendMulticast(new WorkerAvaiableRequest()).Wait();
                });
            }
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
                _cancelMulticast?.Cancel();
                _logger.Log(Tag.WorkerSearcher, $"Worker found: {node.ToString()}");
                _store.StoreWorker(node);
                WorkerFound?.Invoke(node);
                _found = true;
                this.Stop();
            }
        }
    }
}