using System;
using System.Threading.Tasks;

namespace Communication
{
    public interface ICommunicator
    {
        void Close();
        Task Send(Message message);
        Task<T> Receive<T>() where T: Message;
    }
}