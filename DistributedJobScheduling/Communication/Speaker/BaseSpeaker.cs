using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Communication;
using Newtonsoft.Json;

namespace Communication
{
    public abstract class BaseSpeaker : ICommunicator
    {
        private TcpClient _client;
        private NetworkStream _stream;

        protected BaseSpeaker(TcpClient client)
        {
            _stream = _client.GetStream();
        }

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

        public Task<T> Receive<T>() where T: Message
        {
            try
            {
                byte[] bytes = new byte[1024];
                _stream.Read(bytes, 0, bytes.Length);
                return Deserialize<T>(bytes);
            }
            catch
            {
                this.Close();
                Console.WriteLine("Connection closed because of an exception during Receive");
                throw;
            }
        }

        public bool Send<T>(T message) where T: Message
        {
            try
            {
                byte[] bytes = Serialize(message);
                _stream.Write(bytes);
                return true;
            }
            catch
            {
                this.Close();
                Console.WriteLine("Connection closed because of an exception during Send");
                return false;
            }
        }
    }
}