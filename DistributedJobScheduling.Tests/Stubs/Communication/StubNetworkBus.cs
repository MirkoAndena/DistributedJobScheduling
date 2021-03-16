using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Tests.Communication
{
    public class StubNetworkBus
    {
        private Dictionary<string, StubNetworkManager> _networkMap;
        private Dictionary<string, Node.INodeRegistry> _registryMap;
        public float LatencyDeviation {get; set;} = 1.0f;
        private Random _random;

        public StubNetworkBus(int randomSeed)
        {
            _networkMap = new Dictionary<string, StubNetworkManager>();
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
                }
            }
        }

        public async Task SendTo(Node from, Node to, Message message, int timeout)
        {
            await Task.Delay(_random.Next((int)(5*LatencyDeviation),(int)(17*LatencyDeviation)));

            StubNetworkManager receiver = null;

            lock(_networkMap)
            {
                if(_networkMap.ContainsKey(to.IP))
                    receiver = _networkMap[to.IP];
            }

            if(receiver != null)
                receiver.FakeReceive(_registryMap[to.IP].GetOrCreate(from), message);
            else
                await Task.Delay(TimeSpan.FromSeconds(timeout));
        }

        public async Task SendMulticast(Node from, Message message)
        {
            IEnumerable<string> nodesToSendTo;
            lock(_networkMap)
            {
                nodesToSendTo = _networkMap.Keys.Where(x => x != from.IP).OrderBy(n => _random.Next()).AsParallel();
            }

            await Task.Delay(_random.Next((int)(3*LatencyDeviation),(int)(5*LatencyDeviation)));

            //Emulate latency and out of order receive
            Parallel.ForEach(nodesToSendTo, async x => {
                await Task.Delay(_random.Next((int)(5*LatencyDeviation),(int)(17*LatencyDeviation)));
                StubNetworkManager receiver;

                lock(_networkMap)
                {
                    receiver = _networkMap[x];
                }
                
                receiver.FakeReceive(_registryMap[x].GetOrCreate(from), message);
            });
        }
    }
}