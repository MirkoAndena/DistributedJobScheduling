using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Communication
{
    public class BoldSpeaker : Speaker
    {
        public BoldSpeaker(Node interlocutor) : base(new TcpClient(), interlocutor)
        {

        }

        public async Task Connect(int timeout)
        {
            Task connectTask = _client.ConnectAsync(_interlocutor.IP, Listener.PORT);
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeout));
            await Task.WhenAny(connectTask, timeoutTask);

            connectTask.Dispose();
            timeoutTask.Dispose();

            if (timeoutTask.IsCompleted)
            {
                this.Close();
                Console.WriteLine($"An exception occured during connection to {_interlocutor}");
            }
        }
    }
}