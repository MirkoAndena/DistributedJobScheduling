using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Communication.Basic
{
    public class Shouter : IStartable
    {
        const int PORT = 30309;
        const string MULTICAST_IP = "226.122.24.12";

        private Node.INodeRegistry _nodeRegistry;

        public Action<Node, Message> OnMessageReceived;
        private CancellationTokenSource _closeTokenSource;
        private UdpClient _client;
        private ILogger _logger;
        private IPAddress _multicastGroup;

        public Shouter() : this(DependencyInjection.DependencyManager.Get<Node.INodeRegistry>(),
                                DependencyInjection.DependencyManager.Get<ILogger>()) {}
        public Shouter(Node.INodeRegistry nodeRegistry, ILogger logger)
        {
            _nodeRegistry = nodeRegistry;
            _logger = logger;
        }

        public void Start()
        {
            if (_client != null || _closeTokenSource != null)
                Stop();

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress address = host.AddressList[0];

            _client = new UdpClient(new IPEndPoint(IPAddress.Any, PORT));
            _closeTokenSource = new CancellationTokenSource();
            
            _multicastGroup = IPAddress.Parse(MULTICAST_IP);
            _client.JoinMulticastGroup(_multicastGroup);
            _logger.Log(Tag.CommunicationBasic, $"Shouter joined multicast group ({MULTICAST_IP})");
            Receive();
        }

        private async void Receive()
        {
            try
            {
                while (!_closeTokenSource.Token.IsCancellationRequested)
                {
                    UdpReceiveResult result = await _client.ReceiveAsync();
                    Message message = Message.Deserialize<Message>(result.Buffer);
                    Node remote = _nodeRegistry.GetOrCreate(ip: NetworkUtils.GetRemoteIP(_client));
                    _logger.Log(Tag.CommunicationBasic, $"Received {result.Buffer.Length} bytes from MULTICAST");
                    OnMessageReceived?.Invoke(remote, message);
                }
            }
            catch when (_closeTokenSource.Token.IsCancellationRequested) { }
            finally
            {
                _client.Close();
                _client = null;
                _closeTokenSource = null;
                _logger.Warning(Tag.CommunicationBasic, $"Shouter (port {MULTICAST_IP}) stopped");
            }
        }

        public async Task SendMulticast(Message message)
        {
            byte[] content = message.Serialize();
            await _client.SendAsync(content, content.Length, new IPEndPoint(_multicastGroup, PORT));
            _logger.Log(Tag.CommunicationBasic, $"Sent {content.Length} bytes to MULTICAST");
        }

        public void Stop()
        {
            _closeTokenSource?.Cancel();
        }
    }
}