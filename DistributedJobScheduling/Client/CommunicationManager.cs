using System.Diagnostics.Contracts;
using System;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Basic.Speakers;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Serialization;
using static DistributedJobScheduling.Communication.Basic.Node;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Client
{
    public interface IClientCommunication
    {
        event Action<Node, Message> MessageReceived;
        void Send(Message message);
    }

    public class CommunicationManager : IStartable, IClientCommunication
    {
        private BoldSpeaker _speaker;
        private Node _remote;
        private ILogger _logger;
        public event Action<Node, Message> MessageReceived;

        public CommunicationManager()
        {
            _logger = DependencyInjection.DependencyManager.Get<ILogger>();
            var nodeRegistry = DependencyInjection.DependencyManager.Get<INodeRegistry>();
            var configuration = DependencyInjection.DependencyManager.Get<IConfigurationService>();
            var serializer = DependencyInjection.DependencyManager.Get<ISerializer>();

            _remote = nodeRegistry.GetOrCreate(ip: configuration.GetValue<string>("worker"));
            _speaker = new BoldSpeaker(_remote, serializer);
        }

        public void Start()
        {
            Connect();
            _speaker.MessageReceived += OnMessageReceived;
        }

        private void OnMessageReceived(Node node, Message message)
        {
            _logger.Log(Tag.Communication, $"Received message of type {message.GetType().Name}");
            MessageReceived?.Invoke(node, message);
        }

        private void Connect()
        {
            if (_speaker.Running)
                _speaker.Stop();
                
            while (!_speaker.IsConnected)
            {
                try
                {
                    _speaker.Connect(NetworkManager.CLIENT_PORT, 30).Wait();
                    _speaker.Start();
                    _logger.Log(Tag.Communication, "Speaker connected and ready");
                }
                catch (Exception e)
                {
                    _logger.Error(Tag.Communication, $"Client can't connect to remote ({_remote}), retrying...", e);
                }
            }
        }

        public void Stop()
        {
            _speaker.MessageReceived -= OnMessageReceived;
            _speaker.Stop();
        }

        public void Send(Message message)
        {
            lock(_speaker)
            {
                try
                {
                    _speaker.Send(message).Wait();
                    _logger.Log(Tag.Communication, "Message sent successfully");
                }
                catch (Exception e)
                {
                    _logger.Warning(Tag.Communication, "Error during send, retrying", e);
                    this.Connect();
                    this.Send(message);
                }
            }
        }
    }
}