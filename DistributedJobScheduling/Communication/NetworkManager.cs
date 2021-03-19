using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Basic.Speakers;
using DistributedJobScheduling.Communication.Messaging.Ordering;
using DistributedJobScheduling.Communication.Topics;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.LeaderElection;
using DistributedJobScheduling.LeaderElection.KeepAlive;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Communication
{
    public class NetworkManager : ICommunicationManager, ILifeCycle
    {
        private Dictionary<Node, Speaker> _speakers;
        private Listener _listener;
        private Shouter _shouter;
        private Node _me;
        private ILogger _logger;
        private IMessageOrdering _sendOrdering;

        public ITopicOutlet Topics { get; private set; }

        public event Action<Node, Message> OnMessageReceived;

        public NetworkManager() : this(DependencyInjection.DependencyManager.Get<Node.INodeRegistry>(),
                                       DependencyInjection.DependencyManager.Get<Configuration.IConfigurationService>(),
                                       DependencyInjection.DependencyManager.Get<ILogger>()) {}
        public NetworkManager(Node.INodeRegistry nodeRegistry, Configuration.IConfigurationService configurationService, ILogger logger)
        {
            _logger = logger;
            _me = nodeRegistry.GetOrCreate(null, configurationService.GetValue<int>("nodeID"));

            _sendOrdering = new FIFOMessageOrdering(logger);
            _speakers = new Dictionary<Node, Speaker>();

            Topics = new GenericTopicOutlet(this,
                new VirtualSynchronyTopicPublisher()
            );
        }

        private void OnSpeakerCreated(Node node, Speaker speaker)
        {
            _logger.Log(Tag.Communication, $"New speaker created for communicate to {node}");
            _speakers.Add(node, speaker);
            speaker.OnMessageReceived += _OnMessageReceived;
            speaker.StartReceive();
        }

        private void _OnMessageReceived(Node node, Message message)
        {
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
            BoldSpeaker speaker = new BoldSpeaker(node);
            await speaker.Connect(timeout);
            
            if (!_speakers.ContainsKey(node))
                _speakers.Add(node, speaker);

            speaker.StartReceive();
            _logger.Log(Tag.Communication, $"A new speaker to {node} is created and stored");

            return speaker;
        }

        public async Task SendMulticast(Message message)
        {
            await _sendOrdering.OrderedExecute(message, () => _shouter.SendMulticast(message));
        }

        public void Close() 
        {
            _shouter.OnMessageReceived -= _OnMessageReceived;
            _listener.OnSpeakerCreated -= OnSpeakerCreated;

            _shouter.Close();
            _listener.Close();

            _speakers.ForEach(speakerIdPair => 
            {
                speakerIdPair.Value.Close();
                speakerIdPair.Value.OnMessageReceived -= _OnMessageReceived;
                _speakers.Remove(speakerIdPair.Key);
            });

            _logger.Warning(Tag.Communication, "Network manager closed, no further communication can be performed");
        }

        public void Init()
        {
            _speakers.Clear();

            _shouter = new Shouter();
            _shouter.OnMessageReceived += _OnMessageReceived;
            _shouter.Start();

            _listener = new Listener();
            _listener.OnSpeakerCreated += OnSpeakerCreated;
            _listener.Start();
        }

        public void Start()
        {
            _shouter.Start();
            _listener.Start();
        }

        public void Stop() => Close();
    }
}