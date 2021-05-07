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
        public const int GROUP_PORT = 30308;
        public const int CLIENT_PORT = 30408;
        private Dictionary<Node, Speaker> _senders;
        private Dictionary<Node, Speaker> _receivers;
        private Listener _listener, _clientListener;
        private Shouter _shouter;
        private Node _me;
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
            _me = nodeRegistry.GetOrCreate(null, configurationService.GetValue<int>("nodeId"));

            _sendOrdering = new FIFOMessageOrdering(logger);
            _senders = new Dictionary<Node, Speaker>();
            _receivers = new Dictionary<Node, Speaker>();

            Topics = new GenericTopicOutlet(this, logger,
                new VirtualSynchronyTopicPublisher(),
                new JobClientPublisher()
            );

            _shouter = new Shouter(_serializer);
            _shouter.OnMessageReceived += OnMessageReceivedFromSpeakerOrShouter;
            _listener = new Listener(_serializer, GROUP_PORT);
            _listener.SpeakerCreated += OnListenSpeakerCreated;
            _clientListener = new Listener(_serializer, CLIENT_PORT);
            _clientListener.SpeakerCreated += OnClientSpeakerCreated;
        }

        private void OnClientSpeakerCreated(Node node, Speaker speaker)
        {
            OnSpeakerCreated(node, speaker, _receivers);
            lock(_senders)
            {
                // If there is another connection replace the older
                if (_senders.ContainsKey(node))
                {
                    if (_senders[node].IsConnected)
                        _senders[node].Stop();
                    _senders[node] = speaker;
                }
                else
                    _senders.Add(node, speaker);
            }
        }

        private void OnListenSpeakerCreated(Node node, Speaker speaker) => OnSpeakerCreated(node, speaker, _receivers);

        private void OnSendSpeakerCreated(Node node, Speaker speaker) => OnSpeakerCreated(node, speaker, _senders);

        private void OnSpeakerCreated(Node node, Speaker speaker, Dictionary<Node, Speaker> speakerTable)
        {
            lock(speakerTable)
            {
                _logger.Log(Tag.Communication, $"New receiving speaker created for communications with {node}");
                
                if(speakerTable.ContainsKey(node))
                {
                    var oldSpeaker = speakerTable[node];

                    if(oldSpeaker.IsConnected)
                    {
                        _logger.Warning(Tag.Communication, $"Discarded duplicate speaker for {node}");
                        return;
                    }
                        
                    _logger.Log(Tag.Communication, $"Speaker for {node} was due to a previous disconnection, updating");
                    oldSpeaker.MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
                    oldSpeaker.Stop();
                    speakerTable.Remove(node);
                }

                speaker.MessageReceived += OnMessageReceivedFromSpeakerOrShouter;
                speaker.Stopped += (node) => { OnSpeakerStopped(node, speakerTable); };
                speakerTable.Add(node, speaker);
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

        private void OnSpeakerStopped(Node remote, Dictionary<Node, Speaker> speakerTable)
        {
            Node toNotify = null;
            lock(speakerTable)
            {
                if (speakerTable.ContainsKey(remote))
                {
                    speakerTable[remote].MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
                    speakerTable.Remove(remote);
                    toNotify = remote;
                }
            }

            //Notify Deaths only for send links
            if(speakerTable == _senders)
                toNotify?.NotifyDeath();
        }

        public async Task Send(Node node, Message message, int timeout = 30)
        {
            try
            {
                message.SenderID = _me.ID;
                message.ReceiverID = node.ID;

                await _sendOrdering.OrderedExecute(message, async () => {
                    Speaker speaker = await GetSpeakerTo(node, timeout);
                    await speaker.Send(message);
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

        private async Task<Speaker> GetSpeakerTo(Node node, int timeout)
        {
            // Retrieve an already connected speaker
            lock(_senders)
            {
                if (_senders.ContainsKey(node))
                {
                    _logger.Log(Tag.Communication, $"Speaker to {node} is already created");
                    return _senders[node];
                }
            }

            // Create a new speaker and connect to remote
            BoldSpeaker speaker = new BoldSpeaker(node, _serializer);
            await speaker.Connect(GROUP_PORT, timeout);
            
            if(speaker.IsConnected)
                OnSendSpeakerCreated(node, speaker);

            return speaker;
        }

        public async Task SendMulticast(Message message)
        {
            try
            {
                message.SenderID = _me.ID;
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
            _senders.Clear();
            _receivers.Clear();
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

            _senders.ForEach(speakerIdPair => 
            {
                speakerIdPair.Value.Stop();
                speakerIdPair.Value.MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
                _senders.Remove(speakerIdPair.Key);
            });

            _receivers.ForEach(speakerIdPair => 
            {
                speakerIdPair.Value.Stop();
                speakerIdPair.Value.MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
                _receivers.Remove(speakerIdPair.Key);
            });

            _logger.Warning(Tag.Communication, "Network manager closed, no further communication can be performed");
        }

    }
}