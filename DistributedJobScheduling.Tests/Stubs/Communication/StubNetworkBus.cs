using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using DistributedJobScheduling.Communication;
using System.Threading.Tasks.Dataflow;
using DistributedJobScheduling.Communication.Basic;
using System.Threading.Channels;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Queues;

namespace DistributedJobScheduling.Tests.Communication
{
    //FIXME: Channels are not FIFO in this implementation...
    public class StubNetworkBus : IDisposable
    {
        private List<Node> _registeredNodes;
        private ConcurrentDictionary<string, StubNetworkManager> _networkMap;
        private ConcurrentDictionary<(string,string), StubLink> _networkLinks;
        private ConcurrentDictionary<string, Node.INodeRegistry> _registryMap;
        private Random _random;

        private class StubLink
        {
            private Node _a;
            private Node _b;
            private StubNetworkBus _networkBus;
            private CancellationTokenSource _cancellationTokenSource;

            private BlockingCollection<Message> _forwardQueue;
            private BlockingCollection<Message>  _backwardsQueue;

            public StubLink(Node a, Node b, StubNetworkBus networkBus)
            {
                _a = a;
                _b = b;
                _networkBus = networkBus;
                _forwardQueue = new BlockingCollection<Message>();
                _backwardsQueue = new BlockingCollection<Message>();
            }

            public void Enqueue(string fromIP, Message message)
            {
                Console.WriteLine($"ATTEMPT\t({_a.ID} -> {_b.ID}): {message.ToString()}");
                if(_a.IP == fromIP)
                    _forwardQueue.Add(message);
                else
                    _backwardsQueue.Add(message);

                Console.WriteLine($"WROTE\t({_a.ID} -> {_b.ID}): {message.ToString()}");
            }

            public void StartProcessLink()
            {
                if(_cancellationTokenSource != null)
                    _cancellationTokenSource.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                new Thread(() => ProcessChannel(_a, _b, _forwardQueue, _cancellationTokenSource.Token)).Start();
                new Thread(() => ProcessChannel(_b, _a, _backwardsQueue, _cancellationTokenSource.Token)).Start();
            }

            private void ProcessChannel(Node from, Node to, BlockingCollection<Message> queue, CancellationToken cancellationToken)
            {
                try
                {
                    while(!cancellationToken.IsCancellationRequested)
                    {
                        Message message = queue.Take(cancellationToken);
                        _networkBus.FinalizeSendTo(from, to, message);
                    }
                }
                catch(OperationCanceledException) {}
                catch(AggregateException) {}
            }

            public void StopLink()
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = null;
                _forwardQueue = null;
                _backwardsQueue = null;
            }
        }

        public StubNetworkBus(int randomSeed)
        {
            _registeredNodes = new List<Node>();
            _networkMap = new ConcurrentDictionary<string, StubNetworkManager>();
            _networkLinks = new ConcurrentDictionary<(string, string), StubLink>();
            _registryMap = new ConcurrentDictionary<string, Node.INodeRegistry>();
            _random = new Random(randomSeed);
        }

        public void RegisterToNetwork(Node node, Node.INodeRegistry registry, StubNetworkManager communicator)
        {
            lock(_networkMap)
            {
                if(!_networkMap.ContainsKey(node.IP))
                {
                    _networkMap.AddOrUpdate(node.IP, communicator, (a,b) => communicator);
                    _registryMap.AddOrUpdate(node.IP, registry, (a,b) => registry);

                    //Update Links
                    foreach(Node registeredNode in _registeredNodes)
                    {
                        StubLink link = new StubLink(node, registeredNode, this);
                        _networkLinks.AddOrUpdate((node.IP, registeredNode.IP), link, (a,b) => link);
                        _networkLinks.AddOrUpdate((registeredNode.IP, node.IP), link, (a,b) => link);
                        link.StartProcessLink();
                    }
                    _registeredNodes.Add(node);

                    communicator.RegisteredToNetwork(this);
                }
            }
        }

        public void UnregisterFromNetwork(Node node)
        {
            lock(_networkMap)
            {
                if(_networkMap.ContainsKey(node.IP))
                {
                    _networkMap.Remove(node.IP, out _);
                    _registryMap.Remove(node.IP, out _);
                    _registeredNodes.Remove(node);

                    List<(string,string)> _problematicKeys = new List<(string, string)>();
                    foreach(var nodeKey in _networkLinks.Keys)
                        if(nodeKey.Item1 == node.IP || nodeKey.Item2 == node.IP)
                            _problematicKeys.Add(nodeKey);

                    _problematicKeys.ForEach(nodeKey => {
                        _networkLinks[nodeKey].StopLink();
                        _networkLinks.Remove(nodeKey, out _);
                    });
                }
            }
        }

        public void Dispose()
        {
            List<Node> _nodesToRemove = new List<Node>(_registeredNodes);
            foreach(var node in _nodesToRemove)
                UnregisterFromNetwork(node);
        }

        private void FinalizeSendTo(Node from, Node to, Message message)
        {
            StubNetworkManager receiver = null;

            lock(_networkMap)
            {
                if(_networkMap.ContainsKey(to.IP))
                    receiver = _networkMap[to.IP];
            }

            if(receiver != null)
                receiver.FakeReceive(_registryMap[to.IP].GetOrCreate(from), message);
            else
                throw new Exception("Sending messages between undefined nodes!");
        }

        public async Task SendTo(Node from, Node to, Message message, int timeout)
        {
            StubLink selectedLink = null;
            (string,string) linkKey = (from.IP, to.IP);

            await Task.Delay(1); //T_processing

            lock(_networkLinks)
            {
                if(_networkLinks.ContainsKey(linkKey))
                    selectedLink = _networkLinks[linkKey];
            }

            selectedLink.Enqueue(linkKey.Item1, message);
        }

        public async Task SendMulticast(Node from, Message message)
        {
            List<string> nodesToSendTo;
            Console.WriteLine($"{from} attempting lock for {message.TimeStamp} {message}");
            lock(_networkMap)
            {
                nodesToSendTo = _networkMap.Keys.Where(x => x != from.IP).OrderBy(n => _random.Next()).ToList();
            }
            Console.WriteLine($"{from} done lock for {message.TimeStamp} {message}");

            await Task.Yield();

            //Emulate latency and out of order receive
            nodesToSendTo.ForEach(x => {
                Console.WriteLine($"{from} attempting to {x} for {message.TimeStamp} {message}");
                StubLink selectedLink = null;
                (string,string) linkKey = (from.IP, x);

                lock(_networkLinks)
                {
                    if(_networkLinks.ContainsKey(linkKey))
                        selectedLink = _networkLinks[linkKey];
                }
                Console.WriteLine($"{from} selected link for {x} for {message.TimeStamp} {message}");

                selectedLink.Enqueue(linkKey.Item1, message);
            });
        }
    }
}