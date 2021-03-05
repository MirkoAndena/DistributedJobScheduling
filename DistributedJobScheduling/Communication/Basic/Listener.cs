using System;
using System.Net;  
using System.Net.Sockets;
using System.Threading;
using DistributedJobScheduling.Communication.Basic.Speakers;

namespace DistributedJobScheduling.Communication.Basic
{
    public class Listener
    {
        public const int PORT = 30308;
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        public event Action<Node, Speaker> OnSpeakerCreated;

        public void Start()
        {
            if (_listener != null || _cancellationTokenSource != null)
                Close();

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress address = host.AddressList[0];

            _listener = new TcpListener(address, PORT);
            
            try
            {
                _listener.Start();
                Console.WriteLine($"Start listening on port {PORT}");

                _cancellationTokenSource = new CancellationTokenSource();
                AcceptConnection(_cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                Close();
                Console.WriteLine("Listener shutted down because an exception occured:" + e.Message);
            }
        }

        private async void AcceptConnection(CancellationToken token)
        {
            try
            {
                while(!token.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Node interlocutor = Workers.SearchFromIP(client.Client.RemoteEndPoint);
                    Speaker speaker = new Speaker(client, interlocutor);
                    OnSpeakerCreated?.Invoke(interlocutor, speaker);
                }
            }
            catch when (token.IsCancellationRequested) { }
            finally
            {
                _listener.Stop();
                _listener = null;
                _cancellationTokenSource = null;
                Console.WriteLine($"Stop listening on port {PORT}");
            }
        }

        public void Close()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}
