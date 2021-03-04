using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;

namespace Communication
{
    public class Speaker : ICommunicator
    {
        protected TcpClient _client;
        private NetworkStream _stream;

        private CancellationTokenSource _sendToken;
        private CancellationTokenSource _receiveToken;
        private CancellationTokenSource _globalReceiveToken;

        public event Action<Node, Message> OnMessageReceived;

        protected Node _interlocutor;
        public Node Interlocutor => _interlocutor;

        public Speaker(TcpClient client, Node interlocutor)
        {
            _stream = _client.GetStream();
            _interlocutor = interlocutor;
            _sendToken = new CancellationTokenSource();
            _receiveToken = new CancellationTokenSource();
            _globalReceiveToken = new CancellationTokenSource();
        }

        public void AbortSend() => _sendToken.Cancel();
        public void AbortReceive() => _receiveToken.Cancel();

        public void Close()
        {
            if (_client != null)
            {
                _sendToken.Cancel();
                _receiveToken.Cancel();
                _globalReceiveToken.Cancel();
                _client.Close();
            }
        }

        private byte[] Serialize(Message message)
        {
            string json = JsonConvert.SerializeObject(message);
            return Encoding.UTF8.GetBytes(json);
        }

        private T Deserialize<T>(byte[] bytes)
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public async Task<T> Receive<T>() where T: Message
        {
            try
            {
                byte[] bytes = new byte[1024];
                await _stream.ReadAsync(bytes, 0, bytes.Length, _receiveToken.Token);
                return Deserialize<T>(bytes);
            }
            catch
            {
                this.Close();
                Console.WriteLine("Connection closed because of an exception during Receive");
                throw;
            }
        }

        public async void StartReceive()
        {
            while(!_globalReceiveToken.Token.IsCancellationRequested)
            {
                try
                {
                    Message response = await Receive<Message>();
                    OnMessageReceived?.Invoke(_interlocutor, response);
                }
                catch when (_globalReceiveToken.IsCancellationRequested) 
                { 
                    this.Close();
                }
                catch
                {
                    throw;
                }
            }
        }

        public async Task Send(Message message)
        {
            try
            {
                byte[] bytes = Serialize(message);
                await _stream.WriteAsync(bytes, 0, bytes.Length, _sendToken.Token);
            }
            catch
            {
                this.Close();
                Console.WriteLine("Connection closed because of an exception during Send");
                throw;
            }
        }
    }
}