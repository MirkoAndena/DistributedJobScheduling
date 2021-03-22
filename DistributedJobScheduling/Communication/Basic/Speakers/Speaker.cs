using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.LifeCycle;

namespace DistributedJobScheduling.Communication.Basic.Speakers
{
    public class Speaker : IStartable
    {
        protected TcpClient _client;
        private NetworkStream _stream;

        private CancellationTokenSource _sendToken;
        private CancellationTokenSource _receiveToken;
        private CancellationTokenSource _globalReceiveToken;

        protected Node _remote;

        public event Action<Node, Message> OnMessageReceived;

        protected ILogger _logger;

        public Speaker(TcpClient client, Node remote) : this(client, remote, DependencyManager.Get<ILogger>()) {}
        public Speaker(TcpClient client, Node remote, ILogger logger)
        {
            _logger = logger;
            _stream = _client.GetStream();
            _sendToken = new CancellationTokenSource();
            _receiveToken = new CancellationTokenSource();
            _globalReceiveToken = new CancellationTokenSource();
            _remote = remote;
        }

        public void AbortSend() => _sendToken.Cancel();
        public void AbortReceive() => _receiveToken.Cancel();

        public void Stop()
        {
            if (_client != null)
            {
                _sendToken.Cancel();
                _receiveToken.Cancel();
                _globalReceiveToken.Cancel();
                _client.Close();
                _logger.Log(Tag.CommunicationBasic, $"Closed connection to {_remote}");
            }
        }

        private async Task<T> Receive<T>() where T: Message
        {
            try
            {
                byte[] bytes = new byte[1024];
                int bytesRed = await _stream.ReadAsync(bytes, 0, bytes.Length, _receiveToken.Token);
                _logger.Log(Tag.CommunicationBasic, $"Received {bytesRed} bytes from {_remote}");
                return Message.Deserialize<T>(bytes);
            }
            catch (Exception e)
            {
                _logger.Warning(Tag.CommunicationBasic, $"Failed receive from {_remote}", e);
                this.Stop();
                throw;
            }
        }

        public async void Start()
        {
            while(!_globalReceiveToken.Token.IsCancellationRequested)
            {
                try
                {
                    Message response = await Receive<Message>();
                    OnMessageReceived?.Invoke(_remote, response);
                }
                catch when (_globalReceiveToken.IsCancellationRequested) 
                { 
                    _logger.Warning(Tag.CommunicationBasic, $"Stop receiving from {_remote}");
                    this.Stop();
                }
                catch
                {
                    return;
                }
            }
        }

        public async Task Send(Message message)
        {
            try
            {
                byte[] bytes = message.Serialize();
                await _stream.WriteAsync(bytes, 0, bytes.Length, _sendToken.Token);
                _logger.Log(Tag.CommunicationBasic, $"Sent {bytes.Length} bytes to {_remote}");
            }
            catch (Exception e)
            {
                this.Stop();
                _logger.Warning(Tag.CommunicationBasic, $"Failed send to {_remote}", e);
            }
        }
    }
}