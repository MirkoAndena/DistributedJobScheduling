using System;

namespace Communication
{
    public interface ICommunicator
    {
        void Close();
        bool Send(Message message);
        bool ReceiveCallBack(Action<Message> callback);
    }
}