using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using DistributedJobScheduling.Communication;
using System.Threading.Tasks.Dataflow;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Tests.Communication
{
    //FIXME: Channels are not FIFO in this implementation...
    public class StubNetworkBus
    {
        private List<Node> _registeredNodes;
        private Dictionary<string, StubNetworkManager> _networkMap;
        private Dictionary<(string,string), StubLink> _networkLinks;
        private Dictionary<string, Node.INodeRegistry> _registryMap;
        private Random _random;

        private class StubLink
        {
            private Node _a;
            private Node _b;
            private StubNetworkBus _networkBus;
            private CancellationTokenSource _cancellationTokenSource;

            private AsyncQueue<Message> _forwardQueue;
            private AsyncQueue<Message> _backwardsQueue;

            public StubLink(Node a, Node b, StubNetworkBus networkBus)
            {
                _a = a;
                _b = b;
                _networkBus = networkBus;
                _forwardQueue = new AsyncQueue<Message>();
                _backwardsQueue = new AsyncQueue<Message>();
            }

            public void Enqueue(string fromIP, Message message)
            {
                if(_a.IP == fromIP)
                    _forwardQueue.Enqueue(message);
                else
                    _backwardsQueue.Enqueue(message);
            }

            public async void ProcessLink()
            {
                if(_cancellationTokenSource != null)
                    _cancellationTokenSource.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                await Task.WhenAny(
                            Task.Run(async () => {await foreach(Message message in _forwardQueue.WithCancellation(_cancellationTokenSource.Token))
                                _networkBus.FinalizeSendTo(_a, _b, message);
                            }),
                            Task.Run(async () => {await foreach(Message message in _backwardsQueue.WithCancellation(_cancellationTokenSource.Token))
                                _networkBus.FinalizeSendTo(_b, _a, message);
                            }));
                throw new Exception("Link collapsed!");
            }
        }

        public StubNetworkBus(int randomSeed)
        {
            _registeredNodes = new List<Node>();
            _networkMap = new Dictionary<string, StubNetworkManager>();
            _networkLinks = new Dictionary<(string, string), StubLink>();
            _registryMap = new Dictionary<string, Node.INodeRegistry>();
            _random = new Random(randomSeed);
        }

        public void RegisterToNetwork(Node node, Node.INodeRegistry registry, StubNetworkManager communicator)
        {
            lock(_networkMap)
            {
                if(!_networkMap.ContainsKey(node.IP))
                {
                    _networkMap.Add(node.IP, communicator);
                    _registryMap.Add(node.IP, registry);

                    //Update Links
                    foreach(Node registeredNode in _registeredNodes)
                    {
                        StubLink link = new StubLink(node, registeredNode, this);
                        _networkLinks.Add((node.IP, registeredNode.IP), link);
                        _networkLinks.Add((registeredNode.IP, node.IP), link);
                        link.ProcessLink();
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
                    _networkMap.Remove(node.IP);
                    _registryMap.Remove(node.IP);
                    //TODO: Close links
                }
            }
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
            IEnumerable<string> nodesToSendTo;
            lock(_networkMap)
            {
                nodesToSendTo = _networkMap.Keys.Where(x => x != from.IP).OrderBy(n => _random.Next()).AsParallel();
            }

            await Task.Delay(1);
            
            //Emulate latency and out of order receive
            Parallel.ForEach(nodesToSendTo, x => {

                StubLink selectedLink = null;
                (string,string) linkKey = (from.IP, x);

                lock(_networkLinks)
                {
                    if(_networkLinks.ContainsKey(linkKey))
                        selectedLink = _networkLinks[linkKey];
                }

                selectedLink.Enqueue(linkKey.Item1, message);
            });
        }
    }
}