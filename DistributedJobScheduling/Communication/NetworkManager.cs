using System.Runtime.Serialization;
using System.Reflection.PortableExecutable;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Basic.Speakers;
using DistributedJobScheduling.Communication.Messaging.Ordering;
using DistributedJobScheduling.Communication.Topics;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;

namespace DistributedJobScheduling.Communication
{
    public class NetworkManager : ICommunicationManager, IStartable
    {
        private Dictionary<Node, Speaker> _speakers;
        private Listener _listener;
        private Shouter _shouter;
        private Node _me;
        private ILogger _logger;
        private ISerializer _serializer;
        private IMessageOrdering _sendOrdering;
        private Node.INodeRegistry _registry;

        public ITopicOutlet Topics { get; private set; }

        public event Action<Node, Message> OnMessageReceived;

        public NetworkManager(ISerializer serializer) : this(DependencyInjection.DependencyManager.Get<Node.INodeRegistry>(),
                                       DependencyInjection.DependencyManager.Get<Configuration.IConfigurationService>(),
                                       DependencyInjection.DependencyManager.Get<ILogger>(),
                                       serializer) {}
        public NetworkManager(Node.INodeRegistry nodeRegistry, Configuration.IConfigurationService configurationService, ILogger logger, ISerializer serializer)
        {
            _serializer = serializer;
            _registry = nodeRegistry;
            _logger = logger;
            _me = nodeRegistry.GetOrCreate(null, configurationService.GetValue<int>("nodeId"));

            _sendOrdering = new FIFOMessageOrdering(logger);
            _speakers = new Dictionary<Node, Speaker>();

            Topics = new GenericTopicOutlet(this,
                new VirtualSynchronyTopicPublisher()
            );

            _shouter = new Shouter(_serializer);
            _shouter.OnMessageReceived += _OnMessageReceived;
            _listener = new Listener(_serializer);
            _listener.SpeakerCreated += OnSpeakerCreated;
        }

        private void OnSpeakerCreated(Node node, Speaker speaker)
        {
            _logger.Log(Tag.Communication, $"New speaker created for communications with {node}");
            
            if(_speakers.ContainsKey(node))
            {
                var oldSpeaker = _speakers[node];

                if(oldSpeaker.IsConnected)
                {
                    _logger.Warning(Tag.Communication, $"Discarded duplicate speaker for {node}");
                    return;
                }
                    
                _logger.Log(Tag.Communication, $"Speaker for {node} was due to a previous disconnection, updating");
                oldSpeaker.OnMessageReceived -= _OnMessageReceived;
                oldSpeaker.Stop();
                _speakers.Remove(node);
            }

            speaker.OnMessageReceived += _OnMessageReceived;
            _speakers.Add(node, speaker);
            speaker.Start();
        }

        private void _OnMessageReceived(Node node, Message message)
        {
            if (!node.ID.HasValue && message.SenderID.HasValue)
                _registry.UpdateNodeID(node, message.SenderID.Value);

            _logger.Log(Tag.Communication, $"Received message of type {message.GetType()} from {node.ToString()}");
            OnMessageReceived?.Invoke(node, message);
        }

        public async Task Send(Node node, Message message, int timeout = 30)
        {
            Speaker speaker = await GetSpeakerTo(node, timeout);
            message.SenderID = _me.ID;
            message.ReceiverID = node.ID;
            await _sendOrdering.OrderedExecute(message, () => speaker.Send(message));
        }

        private async Task<Speaker> GetSpeakerTo(Node node, int timeout)
        {
            // Retrieve an already connected speaker
            if (_speakers.ContainsKey(node))
            {
                _logger.Log(Tag.Communication, $"Speaker to {node} is already created");
                return _speakers[node];
            }

            // Create a new speaker and connect to remote
            BoldSpeaker speaker = new BoldSpeaker(node, _serializer);
            await speaker.Connect(timeout);
            
            if (!_speakers.ContainsKey(node))
                _speakers.Add(node, speaker);

            speaker.Start();
            _logger.Log(Tag.Communication, $"A new speaker to {node} is created and stored");

            return speaker;
        }

        public async Task SendMulticast(Message message)
        {
            message.SenderID = _me.ID;
            await _sendOrdering.OrderedExecute(message, () => _shouter.SendMulticast(message));
        }

        public void Start()
        {
            _listener.Start();
            _speakers.Clear();
            _shouter.Start();
        }
        
        public void Stop() 
        {
            _shouter.OnMessageReceived -= _OnMessageReceived;
            _listener.SpeakerCreated -= OnSpeakerCreated;

            _shouter.Stop();
            _listener.Stop();

            _speakers.ForEach(speakerIdPair => 
            {
                speakerIdPair.Value.Stop();
                speakerIdPair.Value.OnMessageReceived -= _OnMessageReceived;
                _speakers.Remove(speakerIdPair.Key);
            });

            _logger.Warning(Tag.Communication, "Network manager closed, no further communication can be performed");
        }

    }
}