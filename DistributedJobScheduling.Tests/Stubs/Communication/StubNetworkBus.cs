using System.Collections.Generic;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Routines;
using Communication;

namespace Tests.Communication
{
    public class StubNetworkBus
    {
        private static StubNetworkBus _instance;
        public static StubNetworkBus Instance => (_instance ??= new StubNetworkBus());

        private Dictionary<Node, ICommunicator> _networkMap;

        public StubNetworkBus()
        {
            _networkMap = new Dictionary<Node, ICommunicator>();
        }

        public void RegisterToNetwork(Node node, ICommunicator communicator)
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