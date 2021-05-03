using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;

namespace DistributedJobScheduling.Communication.Basic.Speakers
{
    public class BoldSpeaker : Speaker
    {
        private CancellationTokenSource _connectToken;

        public BoldSpeaker(Node remote, ISerializer serializer) : base(new TcpClient(), remote, serializer)
        {
            
        }

        public async Task Connect(int port, int timeout)
        {
            if(_connectToken != null)
                _connectToken.Cancel();
            _connectToken = new CancellationTokenSource();

            try
            {
                _connectToken.CancelAfter(TimeSpan.FromSeconds(timeout));

                await _client.ConnectAsync(_remote.IP, port, _connectToken.Token);
                _connectToken.Token.ThrowIfCancellationRequested();
                
                _logger.Log(Tag.CommunicationBasic, $"Connected to {_remote} [hash: {_remote.GetHashCode()}]");
                _stream = _client.GetStream();
            }
            catch (ObjectDisposedException)
            {
                this.Stop();
                _logger.Warning(Tag.CommunicationBasic, $"Failed connecting to {_remote} because communication is closed");
                return;
            }
            catch (Exception ex)
            {
                _logger.Warning(Tag.CommunicationBasic, $"Connection to {_remote} failed because of timeout", ex);
                this.Stop();
                return;
            }
        }
    }
}