using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Communication
{
    public class Speaker : BaseSpeaker
    {
        private CancellationTokenSource _connectToken;

        public Speaker() : base(new TcpClient())
        {
            _connectToken = new CancellationTokenSource();
        }

        public async Task Connect(Node node)
        {
            try
            {
                _interlocutor = node;
                await _client.ConnectAsync(node.IP, Listener.PORT, _connectToken.Token);
            }
            catch
            {
                this.Close();
                Console.WriteLine($"An exception occured during connection to {node}");
                throw;
            }
        }
    }
}