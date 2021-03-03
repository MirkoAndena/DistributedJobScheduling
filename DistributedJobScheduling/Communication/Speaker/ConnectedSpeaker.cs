using System;
using System.Net.Sockets;

namespace Communication
{
    public class ConnectedSpeaker : BaseSpeaker
    {
        private Action<Node> _closeCallback;
        
        public ConnectedSpeaker(TcpClient tcpClient, Node interlocutor, Action<Node> closeCallback) : base(tcpClient)
        {
            this._closeCallback = closeCallback;
            this._interlocutor = interlocutor;
        }

        public new void Close()
        {
            base.Close();
            _closeCallback?.Invoke(_interlocutor);
        }
    }
}