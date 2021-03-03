using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Communication
{
    public class Speaker : BaseSpeaker
    {
        public Speaker() : base(new TcpClient())
        {

        }

        public async Task Connect(Node node, int timeout)
        {
            _interlocutor = node;
            Task connectTask = _client.ConnectAsync(node.IP, Listener.PORT);
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeout));
            await Task.WhenAny(connectTask, timeoutTask);

            connectTask.Dispose();
            timeoutTask.Dispose();

            if (timeoutTask.IsCompleted)
            {
                this.Close();
                Console.WriteLine($"An exception occured during connection to {node}");
            }
        }
    }
}