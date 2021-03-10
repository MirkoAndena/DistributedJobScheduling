using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedJobScheduling.Communication.Basic.Speakers
{
    public class BoldSpeaker : Speaker
    {
        public BoldSpeaker(Node remote) : base(new TcpClient(), remote)
        {

        }

        public async Task Connect(int timeout)
        {
            Task connectTask = _client.ConnectAsync(_remote.IP, Listener.PORT);
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeout));
            await Task.WhenAny(connectTask, timeoutTask);

            connectTask.Dispose();
            timeoutTask.Dispose();

            if (timeoutTask.IsCompleted)
            {
                this.Close();
                Console.WriteLine($"An exception occured during connection to {_remote}");
            }
        }
    }
}