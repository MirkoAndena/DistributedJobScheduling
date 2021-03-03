using System;
using System.Net.Sockets;

namespace Communication
{
    public class ConnectedSpeaker : BaseSpeaker
    {
        private Action _closeCallback;
        
        public ConnectedSpeaker(TcpClient tcpClient, Action closeCallback) : base(tcpClient)
        {
            this._closeCallback = closeCallback;
        }

        public new void Close()
        {
            base.Close();
            _closeCallback?.Invoke();
        }
    }
}