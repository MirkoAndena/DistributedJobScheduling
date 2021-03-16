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
        public float LatencyDeviation {get; set;} = 1.0f;
        private Random _random;

        public StubNetworkBus(int randomSeed)
        {
            _networkMap = new Dictionary<string, StubNetworkManager>();
            _random = new Random(randomSeed);
        }

        public void RegisterToNetwork(Node node, StubNetworkManager communicator)
        {
            if(!_networkMap.ContainsKey(node.IP))
            {
                _networkMap.Add(node.IP, communicator);
                communicator.RegisteredToNetwork(this);
            }
        }

        public void UnregisterFromNetwork(Node node)
        {
            if(_networkMap.ContainsKey(node.IP))
                _networkMap.Remove(node.IP);
        }

        public async Task SendTo(Node from, Node to, Message message, int timeout)
        {
            await Task.Delay(_random.Next((int)(5*LatencyDeviation),(int)(17*LatencyDeviation)));

            if(_networkMap.ContainsKey(to.IP))
                _networkMap[to.IP].FakeReceive(from, message);
            else
                await Task.Delay(TimeSpan.FromSeconds(timeout));
        }

        public async Task SendMulticast(Node from, Message message)
        {
            var nodesToSendTo = _networkMap.Keys.Where(x => x != from.IP).OrderBy(n => _random.Next()).AsParallel();

            await Task.Delay(_random.Next((int)(3*LatencyDeviation),(int)(5*LatencyDeviation)));

            //Emulate latency and out of order receive
            Parallel.ForEach(nodesToSendTo, async x => {
                await Task.Delay(_random.Next((int)(5*LatencyDeviation),(int)(17*LatencyDeviation)));
                _networkMap[x].FakeReceive(from, message);
            });
        }
    }
}