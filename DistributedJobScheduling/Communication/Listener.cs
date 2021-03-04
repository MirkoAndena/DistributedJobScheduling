﻿using System;
using System.Collections.Generic;
using System.Net;  
using System.Net.Sockets;
using System.Threading;

namespace Communication
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

        private Node SearchFromIP(EndPoint endPoint)
        {
            string ip = ((IPEndPoint)endPoint).Address.ToString();
            if (ip == Workers.Instance.Coordinator.IP) return Workers.Instance.Coordinator;
            foreach (Node node in Workers.Instance.Others.Values) 
                if (ip == node.IP)
                    return node;
            throw new Exception($"Received a connection request from someone that's not in the group: ${ip}");
        }

        private async void AcceptConnection(CancellationToken token)
        {
            try
            {
                while(!token.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Speaker speaker = new Speaker(client);
                    Node interlocutor = SearchFromIP(client.Client.RemoteEndPoint);
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
