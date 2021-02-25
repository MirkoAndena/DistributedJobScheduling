using System;
using System.Net.Sockets;
using System.Text;

namespace Communication
{
    class Speaker : ICommunicable
    {
        private Socket _socket;
        private Action _closeCallback;

        public Speaker(Socket socket, Action closeCallback)
        {
            this._socket = socket;
            this._closeCallback = closeCallback;
        }

        public void Close()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            _closeCallback.Invoke();
        }

        public IMessage Receive()
        {
            throw new System.NotImplementedException();
        }

        public void Send(IMessage message)
        {
            try
            {
                byte[] byteData = Encoding.UTF8.GetBytes(message.ToString());
                _socket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, result => _socket.EndSend(result), null);
            }
            catch
            {
                this.Close();
                Console.WriteLine("Connection closed because of an exception during Send");
            }
        }
    }
}