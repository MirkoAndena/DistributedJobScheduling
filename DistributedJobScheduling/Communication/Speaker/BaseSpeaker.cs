using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;

namespace Communication
{
    public abstract class BaseSpeaker : ICommunicator
    {
        protected TcpClient _client;
        private NetworkStream _stream;

        private CancellationTokenSource _sendToken;
        private CancellationTokenSource _receiveToken;

        protected Node _interlocutor;
        public Node Interlocutor => _interlocutor;

        protected BaseSpeaker(TcpClient client)
        {
            _stream = _client.GetStream();
            _sendToken = new CancellationTokenSource();
            _receiveToken = new CancellationTokenSource();
        }

        public void AbortSend() => _sendToken.Cancel();
        public void AbortReceive() => _receiveToken.Cancel();

        public void Close()
        {
            if (_client != null)
                _client.Close();
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