using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Communication.Basic.Speakers
{
    public class BoldSpeaker : Speaker
    {
        private CancellationTokenSource _connectToken;

        public BoldSpeaker(Node remote) : base(new TcpClient(), remote)
        {
            
        }

        public async Task Connect(int timeout)
        {
            if(_connectToken != null)
                _connectToken.Cancel();
            _connectToken = new CancellationTokenSource();
            _connectToken.Token.ThrowIfCancellationRequested();
            _connectToken.CancelAfter(TimeSpan.FromSeconds(timeout));

            try
            {
                await _client.ConnectAsync(_remote.IP, Listener.PORT, _connectToken.Token);
                _logger.Log(Tag.CommunicationBasic, $"Connected to {_remote}");
                _stream = _client.GetStream();
            }
            catch (OperationCanceledException)
            {
                _logger.Warning(Tag.CommunicationBasic, $"Connection to {_remote} failed because of timeout");
                this.Stop();
            }
        }
    }
}