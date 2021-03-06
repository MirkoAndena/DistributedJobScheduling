using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedJobScheduling.Communication.Basic.Speakers
{
    public class BoldSpeaker : Speaker
    {
        public BoldSpeaker() : base(new TcpClient())
        {

        }

        public async Task Connect(string ip, int timeout)
        {
            Task connectTask = _client.ConnectAsync(ip, Listener.PORT);
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeout));
            await Task.WhenAny(connectTask, timeoutTask);

            connectTask.Dispose();
            timeoutTask.Dispose();

            if (timeoutTask.IsCompleted)
            {
                this.Close();
                Console.WriteLine($"An exception occured during connection to {ip}");
            }
        }
    }
}