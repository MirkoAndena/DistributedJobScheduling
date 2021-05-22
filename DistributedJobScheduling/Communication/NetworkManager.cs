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
using DistributedJobScheduling.JobAssignment;
using DistributedJobScheduling.Client;

namespace DistributedJobScheduling.Communication
{
    public class NetworkManager : ICommunicationManager, IStartable
    {
        public const int PORT = 30308;
        private Dictionary<Node, Speaker> _speakers;
        private Listener _listener;
        private Shouter _shouter;
        private int _sender;
        private ILogger _logger;
        private ISerializer _serializer;
        private IMessageOrdering _sendOrdering;
        private Node.INodeRegistry _registry;

        public ITopicOutlet Topics { get; private set; }

        public event Action<Node, Message> OnMessageReceived;

        public NetworkManager() : this(DependencyInjection.DependencyManager.Get<Node.INodeRegistry>(),
                                       DependencyInjection.DependencyManager.Get<Configuration.IConfigurationService>(),
                                       DependencyInjection.DependencyManager.Get<ILogger>(),
                                       DependencyInjection.DependencyManager.Get<ISerializer>()) {}
        public NetworkManager(Node.INodeRegistry nodeRegistry, Configuration.IConfigurationService configurationService, ILogger logger, ISerializer serializer)
        {
            _serializer = serializer;
            _registry = nodeRegistry;
            _logger = logger;
            _sender = configurationService.GetValue<int>("nodeId");

            _sendOrdering = new FIFOMessageOrdering(logger);
            _speakers = new Dictionary<Node, Speaker>();

            Topics = new GenericTopicOutlet(this, logger,
                new VirtualSynchronyTopicPublisher(),
                new JobClientPublisher()
            );

            _registry.NodeCreated += OnNodeCreated;

            _shouter = new Shouter(_serializer);
            _shouter.OnMessageReceived += OnMessageReceivedFromSpeakerOrShouter;
            _listener = new Listener(_serializer, PORT);
            _listener.SpeakerCreated += OnSpeakerCreated;
        }

        private async void OnNodeCreated(Node node)
        {
            // Do not connect with myself
            if (node.ID.Value == _sender)
                return;

            // Open connection only with lower nodes
            if (node.ID.Value < _sender)
            {
                BoldSpeaker speaker = new BoldSpeaker(node, _serializer);
                await speaker.Connect(PORT, 30);

                speaker.MessageReceived += OnMessageReceivedFromSpeakerOrShouter;
                speaker.Stopped += OnSpeakerStopped;
                speaker.Start();

                _speakers.Add(node, speaker);
            }
        }

        private void OnSpeakerCreated(Node node, Speaker speaker)
        {
            lock(_speakers)
            {
                _logger.Log(Tag.Communication, $"New receiving speaker created for communications with {node}");
                
                if(_speakers.ContainsKey(node))
                {
                    var oldSpeaker = _speakers[node];

                    if(oldSpeaker.IsConnected)
                    {
                        _logger.Warning(Tag.Communication, $"Discarded duplicate speaker for {node}");
                        return;
                    }
                        
                    _logger.Log(Tag.Communication, $"Speaker for {node} was due to a previous disconnection, updating");
                    oldSpeaker.MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
                    oldSpeaker.Stop();
                    _speakers.Remove(node);
                }

                speaker.MessageReceived += OnMessageReceivedFromSpeakerOrShouter;
                speaker.Stopped += (node) => { OnSpeakerStopped(node); };
                _speakers.Add(node, speaker);
            }

            speaker.Start();
        }

        private void OnMessageReceivedFromSpeakerOrShouter(Node node, Message message)
        {
            if (message.SenderID.HasValue && node.ID != message.SenderID)
                _registry.UpdateNodeID(node, message.SenderID.Value);

            _logger.Log(Tag.Communication, $"Received message of type {message.GetType()} from {node.ToString()}");
            try
            {
                OnMessageReceived?.Invoke(node, message);
            }
            catch(Exception ex) {
                _logger.Error(Tag.Communication, $"Exception generated while handling {message.GetType()} from {node.ToString()}", ex);
            }
        }

        private void OnSpeakerStopped(Node remote)
        {
            lock(_speakers)
            {
                if (_speakers.ContainsKey(remote))
                {
                    _speakers[remote].MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
                    _speakers.Remove(remote);
                    remote.NotifyDeath();
                }
            }
        }

        public async Task Send(Node node, Message message, int timeout = 30)
        {
            try
            {
                message.SenderID = _sender;
                message.ReceiverID = node.ID;

                await _sendOrdering.OrderedExecute(message, async () => {
                    if (!_speakers.ContainsKey(node))
                        throw new Exception($"Speakers does not contain a speaker for node {node}");
                    await _speakers[node].Send(message);
                });
            }
            catch
            {
                throw;
            }
            finally
            {
                if(message != null && message.TimeStamp.HasValue)
                    _sendOrdering.Observe(message);
            }
        }

        public async Task SendMulticast(Message message)
        {
            try
            {
                message.SenderID = _sender;
                await _sendOrdering.OrderedExecute(message, () => _shouter.SendMulticast(message));
            }
            catch
            {
                throw;
            }
            finally
            {
                if(message != null && message.TimeStamp.HasValue)
                    _sendOrdering.Observe(message);
            }
        }

        public void Start()
        {
            _listener.Start();
            _clientListener.Start();
            _speakers.Clear();
            _shouter.Start();
        }
        
        public void Stop() 
        {
            _shouter.OnMessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
            _listener.SpeakerCreated -= OnListenSpeakerCreated;
            _clientListener.SpeakerCreated -= OnClientSpeakerCreated;

            _shouter.Stop();
            _listener.Stop();
            _clientListener.Stop();

            _speakers.ForEach(speakerIdPair => 
            {
                speakerIdPair.Value.Stop();
                speakerIdPair.Value.MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
                _speakers.Remove(speakerIdPair.Key);
            });

            _logger.Warning(Tag.Communication, "Network manager closed, no further communication can be performed");
        }

    }
}