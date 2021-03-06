using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedJobScheduling.Communication.Basic
{
    public class Shouter
    {
        const int PORT = 30309;
        const string MULTICAST_IP = "226.122.24.12";

        public Action<Node, Message> OnMessageReceived;
        private CancellationTokenSource _closeTokenSource;
        private UdpClient _client;

        public void Start()
        {
            if (_client != null || _closeTokenSource != null)
                Close();

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress address = host.AddressList[0];

            _client = new UdpClient(new IPEndPoint(address, PORT));
            _closeTokenSource = new CancellationTokenSource();
            
            _client.JoinMulticastGroup(IPAddress.Parse(MULTICAST_IP));
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
                    Node remote = new Node(NetworkUtils.GetRemoteIP(_client));
                    OnMessageReceived?.Invoke(remote, message);
                }
            }
            catch when (_closeTokenSource.Token.IsCancellationRequested) { }
            finally
            {
                _client.Close();
                _client = null;
                _closeTokenSource = null;
                Console.WriteLine($"Stop listening multicast on port {PORT}");
            }
        }

        public async Task SendMulticast(Message message)
        {
            byte[] content = message.Serialize();
            await _client.SendAsync(content, content.Length);
        }

        public void Close()
        {
            _closeTokenSource?.Cancel();
        }
    }
}