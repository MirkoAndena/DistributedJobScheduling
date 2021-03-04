using System.Collections.Generic;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Tests.Communication
{
    public class StubNetworkBus
    {
        private static StubNetworkBus _instance;
        public static StubNetworkBus Instance => (_instance ??= new StubNetworkBus());

        private Dictionary<Node, ICommunicationManager> _networkMap;

        public StubNetworkBus()
        {
            _networkMap = new Dictionary<Node, ICommunicationManager>();
        }

        public void RegisterToNetwork(Node node, ICommunicationManager communicator)
        {
            if(!_networkMap.ContainsKey(node))
                _networkMap.Add(node, communicator);
        }

        public void UnregisterFromNetwork(Node node)
        {
            if(_networkMap.ContainsKey(node))
                _networkMap.Remove(node);
        }

        public void SendTo(Node to, Message message)
        {
            //if(_networkMap.ContainsKey(to))
            //    _networkMap[to].
        }
    }
}