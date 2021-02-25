using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Routines;

namespace Communication
{
    public class Speaker : ICommunicator
    {
        private Socket _socket;
        private Action _closeCallback;
        private byte[] _buffer;
        private Routine _routine;

        public Speaker(Socket socket, Action closeCallback, Routine routine)
        {
            this._socket = socket;
            this._closeCallback = closeCallback;
            this._buffer = new byte[1024];
            this._routine = routine;
            this._routine.Communicator = this;
        }

        public static void CreateAndRun(string host, Routine routine)
        {
            Speaker speaker = new Speaker(null, null, routine);
            speaker.Connect(host);
        }

        public void Connect(string host)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Tcp);
            _socket.BeginConnect(host, Listener.PORT, result => 
            {
                _socket.EndConnect(result);
                _routine.Communicator = this;
                _routine.Start();
            }, null);
        }

        public void Close()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            _closeCallback?.Invoke();
        }

        public bool ReceiveCallBack(Action<Message> callback)
        {
            try
            {
                _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, result => Receive(result, callback), null);
                return true;
            }
            catch
            {
                this.Close();
                Console.WriteLine("Connection closed because of an exception during Receive");
                return false;
            }
        }

        private void Receive(IAsyncResult asyncResult, Action<Message> callback)
        {
            int bytesReceived = _socket.EndReceive(asyncResult);
            if (bytesReceived > 0)
            {
                string json = Encoding.UTF8.GetString(_buffer);
                Message message = JsonSerializer.Deserialize<Message>(json, null);
                callback.Invoke(message);
            }
            ReceiveCallBack(callback);
        }

        public bool Send(Message message)
        {
            try
            {
                string json = JsonSerializer.Serialize<Message>(message, null);
                byte[] byteData = Encoding.UTF8.GetBytes(json);
                _socket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, result => _socket.EndSend(result), null);
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