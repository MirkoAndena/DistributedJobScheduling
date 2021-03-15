using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Logging;

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
                _logger.Warning(Tag.CommunicationBasic, $"Connection to {_remote} failed because of timeout");
                this.Close();
            }
            else
            {
                _logger.Log(Tag.CommunicationBasic, $"Connected to {_remote}");
            }
        }
    }
}