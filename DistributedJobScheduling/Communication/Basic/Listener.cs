using System;
using System.Net;  
using System.Net.Sockets;
using System.Threading;
using DistributedJobScheduling.Communication.Basic.Speakers;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;

namespace DistributedJobScheduling.Communication.Basic
{
    public class Listener : IStartable
    {
        private Node.INodeRegistry _nodeRegistry;

        public const int PORT = 30308;
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        public event Action<Node, Speaker> SpeakerCreated;
        private int _myID;
        protected ILogger _logger;
        private ISerializer _serializer;

        public Listener(ISerializer serializer) : this(DependencyManager.Get<Node.INodeRegistry>(),
                                 DependencyManager.Get<Configuration.IConfigurationService>(),
                                 DependencyManager.Get<ILogger>(), serializer) {}
        public Listener(Node.INodeRegistry nodeRegistry, Configuration.IConfigurationService configurationService, ILogger logger, ISerializer serializer)
        {
            _serializer = serializer;
            _nodeRegistry = nodeRegistry;
            _myID = configurationService.GetValue<int>("nodeId");
            _logger = logger;
        }

        public void Start()
        {
            if (_listener != null || _cancellationTokenSource != null)
                Stop();

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress address = host.AddressList[0];
            
            Node me = _nodeRegistry.GetNode(_myID);
            _nodeRegistry.UpdateNodeIP(me, address.ToString());

            _listener = new TcpListener(address, PORT);
            
            try
            {
                _listener.Start();
                _logger.Log(Tag.CommunicationBasic, $"Start listening on {address.ToString()}:{PORT}");
            }
            catch (Exception e)
            {
                _logger.Error(Tag.CommunicationBasic, "Listener unable to start", e);
                Stop();
            }
            
            _cancellationTokenSource = new CancellationTokenSource();
            AcceptConnection(_cancellationTokenSource.Token);
        }

        private async void AcceptConnection(CancellationToken token)
        {
            try
            {
                while(!token.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Node remote = _nodeRegistry.GetOrCreate(ip: NetworkUtils.GetRemoteIP(client));
                    _logger.Log(Tag.CommunicationBasic, $"Accepted connection request to {remote}");
                    Speaker speaker = new Speaker(client, remote, _serializer);
                    SpeakerCreated?.Invoke(remote, speaker);
                }
            }
            catch (OperationCanceledException)
            { 
                _logger.Warning(Tag.CommunicationBasic, $"Listener (port {PORT}) stopped manually with CancellationToken");
            }
            catch (AggregateException)
            { 
                _logger.Warning(Tag.CommunicationBasic, $"Listener (port {PORT}) stopped manually with AggregateCancellationToken");
            }
            finally
            {
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener = null;
                    _cancellationTokenSource = null;
                    _logger.Warning(Tag.CommunicationBasic, $"Listener (port {PORT}) stopped");
                }
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}
