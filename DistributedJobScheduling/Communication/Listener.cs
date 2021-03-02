using System;
using System.Collections.Generic;
using System.Net;  
using System.Net.Sockets;  
using Routines;
using System.Threading;

namespace Communication
{
    public class Listener
    {
        public const int PORT = 30308;
        private TcpListener _listener;
        private Dictionary<int, Speaker> _speakers;
        private int _lastSpeakerIndex;
        private CancellationTokenSource _cancellationTokenSource;
        private Routine _routine;

        public Listener(Routine routine)
        {
            _lastSpeakerIndex = 0;
            _speakers = new Dictionary<int, Speaker>();
            _routine = routine;
        }

        public static Listener CreateAndStart(Routine routine)
        {
            Listener listener = new Listener(routine);
            listener.Start();
            return listener;
        }

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
                    int currentIndex = _lastSpeakerIndex;
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Speaker speaker = new Speaker(socketToRemote, () => _speakers.Remove(currentIndex), _routine);
                    _speakers.Add(currentIndex, speaker);
                    _lastSpeakerIndex++;
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

            using (Dictionary<int, Speaker>.ValueCollection.Enumerator enumerator = _speakers.Values.GetEnumerator())
            {
                while(enumerator.MoveNext())
                    enumerator.Current.Close();
            }
        }
    }
}
