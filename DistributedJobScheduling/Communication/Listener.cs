using System;
using System.Collections.Generic;
using System.Net;  
using System.Net.Sockets;  
using System.Text;  
using System.Threading;  

namespace Communication
{
    public class Listener
    {
        const int PORT = 30308;
        private Socket _socket;
        private Dictionary<int, Speaker> _speakers;
        private int _lastSpeakerIndex;

        public Listener()
        {
            _lastSpeakerIndex = 0;
            _speakers = new Dictionary<int, Speaker>();
        }

        public void Start()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress address = host.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(address, PORT);

            _socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Tcp);
            
            try
            {
                _socket.Bind(endPoint);
                _socket.Listen();
                Console.WriteLine($"Start listening on port {PORT}");

                while(true)
                {
                    _socket.BeginAccept(result => 
                    {
                        int currentIndex = _lastSpeakerIndex;
                        Speaker speaker = new Speaker(_socket.EndAccept(result), () => _speakers.Remove(currentIndex));
                        _speakers.Add(currentIndex, speaker);
                        _lastSpeakerIndex++;
                    }, null);
                }
            }
            catch (Exception e)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
                Console.WriteLine("Listener shutted down because an exception occured:" + e.Message);
            }
        }
    }
}
