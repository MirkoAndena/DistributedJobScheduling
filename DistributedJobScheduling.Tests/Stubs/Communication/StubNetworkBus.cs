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
        private Dictionary<Node, StubNetworkManager> _networkMap;
        private Random _random;

        public StubNetworkBus(int randomSeed)
        {
            _networkMap = new Dictionary<Node, StubNetworkManager>();
            _random = new Random(randomSeed);
        }

        public void RegisterToNetwork(Node node, StubNetworkManager communicator)
        {
            if(!_networkMap.ContainsKey(node))
            {
                _networkMap.Add(node, communicator);
                communicator.RegisteredToNetwork(this);
            }
        }

        public void UnregisterFromNetwork(Node node)
        {
            if(_networkMap.ContainsKey(node))
                _networkMap.Remove(node);
        }

        public async Task SendTo(Node from, Node to, Message message, int timeout)
        {
            await Task.Delay(_random.Next(5,17));

            if(_networkMap.ContainsKey(to))
                _networkMap[to].FakeReceive(from, message);
            else
                await Task.Delay(TimeSpan.FromSeconds(timeout));
        }

        public async Task SendMulticast(Node from,Message message)
        {
            var nodesToSendTo = _networkMap.Keys.Where(x => x != from).OrderBy(n => _random.Next());

            await Task.Delay(_random.Next(5,17));
            nodesToSendTo.ForEach(x => _networkMap[x].FakeReceive(from, message));
        }
    }
}