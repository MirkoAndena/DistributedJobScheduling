using System.Threading;
using System.Collections.Generic;
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
        private ISerializer _serializer;
        private ILogger _logger;

        private SemaphoreSlim _connectionSemaphore;
        private bool _disconnected;

        public event Action<Node, Message> MessageReceived;

        public CommunicationManager()
        {
            _logger = DependencyInjection.DependencyManager.Get<ILogger>();
            _serializer = DependencyInjection.DependencyManager.Get<ISerializer>();
            var nodeRegistry = DependencyInjection.DependencyManager.Get<INodeRegistry>();
            var configuration = DependencyInjection.DependencyManager.Get<IConfigurationService>();

            _remote = nodeRegistry.GetOrCreate(ip: configuration.GetValue<string>("worker"));
            _connectionSemaphore = new SemaphoreSlim(0, 1);
        }

        public void Start() => ConnectIfNot();
        
        public void Stop() => StopSpeaker();

        private void OnMessageReceived(Node node, Message message)
        {
            _logger.Log(Tag.Communication, $"Received message of type {message.GetType().Name}");
            MessageReceived?.Invoke(node, message);
        }

        private async Task ConnectIfNot()
        {
            if (_speaker == null || !_speaker.IsConnected)
            {
                // Safe stop speaker
                StopSpeaker();

                // Create new one
                _speaker = new BoldSpeaker(_remote, _serializer);
                bool connected = await _speaker.Connect(NetworkManager.CLIENT_PORT, 30);
                _logger.Log(Tag.Communication, $"Speaker is connected: {_speaker.IsConnected}");

                if (connected)
                {
                    _speaker.MessageReceived += OnMessageReceived;
                    _speaker.Stopped += OnSpeakerStopped;
                    _speaker.Start();
                    _disconnected = false;
                }
                else
                {
                    _logger.Log(Tag.Communication, $"Wait 10 seconds and retry");
                    Task.Delay(TimeSpan.FromSeconds(10)).Wait();
                    await this.ConnectIfNot();
                }
            }

            // Here the speaker has connected
            _connectionSemaphore.Release();
        }

        private void StopSpeaker()
        {
            if (_speaker != null)
            { 
                _speaker.Stopped -= OnSpeakerStopped;
                _speaker.MessageReceived -= OnMessageReceived;
                _speaker.Stop();
            }
        }

        private void OnSpeakerStopped(Node node)
        {
            _disconnected = true;
            Task.Run(() => ConnectIfNot());
        }

        public void Send(Message message)
        {
            if (_disconnected)
                _connectionSemaphore.Wait();

            try
            {
                _speaker.Send(message).Wait();
                _logger.Log(Tag.Communication, "Message sent successfully");
            }
            catch (Exception e)
            {
                _logger.Warning(Tag.Communication, "Error during send, retrying", e);
                Resend(message);
            }
        }

        private void Resend(Message message)
        {
            this.ConnectIfNot().Wait();
            this.Send(message);
        }
    }
}