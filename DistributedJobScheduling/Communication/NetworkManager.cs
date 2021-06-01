using System.Linq;
using System.Net.Http.Headers;
using System.Xml.Serialization;
using System.Collections.Concurrent;
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
using System.Threading;
using DistributedJobScheduling.Communication.Messaging.Discovery;

namespace DistributedJobScheduling.Communication
{
    public class NetworkManager : ICommunicationManager, IStartable
    {
        public enum SendFailureStrategy { Discard, Reconnect }

        public const int PORT = 30308;
        private Dictionary<Node, Speaker> _speakers;
        private Listener _listener;
        private Shouter _shouter;
        private int _sender;
        private ILogger _logger;
        private ISerializer _serializer;
        private IMessageOrdering _sendOrdering;
        private Node.INodeRegistry _registry;
        private Dictionary<Node, Task> _connectionTasks;
        private Dictionary<Node, Queue<Message>> _notSent;
        private Dictionary<Node, SemaphoreSlim> _reconnectionSemaphores;
        private Thread _pingThread;

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
            _connectionTasks = new Dictionary<Node, Task>();
            _notSent = new Dictionary<Node, Queue<Message>>();
            _reconnectionSemaphores = new Dictionary<Node, SemaphoreSlim>();

            _sendOrdering = new FIFOMessageOrdering(logger);
            _speakers = new Dictionary<Node, Speaker>();

            Topics = new GenericTopicOutlet(this, logger,
                new VirtualSynchronyTopicPublisher(),
                new JobClientPublisher()
            );

            _registry.NodeCreated += ConnectWith;

            _shouter = new Shouter(_serializer);
            _shouter.OnMessageReceived += OnMulticastReceived;
            _listener = new Listener(_serializer, PORT);
            _listener.SpeakerCreated += OnSpeakerCreated;
        }

        private void OnMulticastReceived(Node node, Message message)
        {
            if(!_speakers.ContainsKey(node))
                ConnectWith(node);
            OnMessageReceivedFromSpeakerOrShouter(node, message);
        }

        private void OnNetworkStateReceived(Node node, NetworkStateMessage message)
        {
            if(!_speakers.ContainsKey(node))
                ConnectWith(node);
        }

        private void ConnectWith(Node node)
        {
            if (!node.ID.HasValue)
            {
                _logger.Warning(Tag.Communication, $"Try to connect to {node} that hasn't an ID");
                return;
            }

            lock(_speakers)
            {
                if (_speakers.ContainsKey(node) && _speakers[node].IsConnected)
                {
                    _logger.Log(Tag.Communication, $"Already connected to {node}");
                    return;
                }
            }

            _logger.Log(Tag.Communication, $"Handle connectio with {node}, Do i have to connect with him or not?");

            // Do not connect with myself
            if (node.ID.Value == _sender)
                return;

            // Open connection only with lower nodes
            if (node.ID.Value > _sender)
            {
                _logger.Log(Tag.Communication, $"Node {node} has greater id (mine is {_sender}), creating connection [hash:{node.GetHashCode()}]");
                CreateSpeakerAndConnect(node);
            }
        }

        private void CreateSpeakerAndConnect(Node node)
        {
            lock(_connectionTasks)
            {
                if(_connectionTasks.ContainsKey(node))
                {
                    if(_connectionTasks[node].IsCompleted)
                        _connectionTasks.Remove(node);
                    else
                        return;
                }
                
                _connectionTasks.Add(node, 
                    Task.Run(async () =>
                    {
                        await GetOrCreateSemaphore(node).WaitAsync();

                        lock(_speakers)
                        {
                            if(_speakers.ContainsKey(node))
                            {
                                _logger.Warning(Tag.Communication, "An attempt to connect two times to the same node was made, returning early");
                                GetOrCreateSemaphore(node).Release();
                                return;
                            }
                        }
                        
                        BoldSpeaker speaker = new BoldSpeaker(node, _serializer);
                        await speaker.Connect(PORT, 30);

                        if (speaker.IsConnected)
                        {
                            speaker.MessageReceived += OnMessageReceivedFromSpeakerOrShouter;
                            speaker.Stopped += OnSpeakerStopped;
                            speaker.Start();

                            lock(_speakers)
                            {
                                _speakers.Add(node, speaker);
                            }
                            _logger.Log(Tag.Communication, $"Speaker to {node} created");

                            await Send(node, new HelloMessage(), ignoreSemaphore: true);
                            SendEnqueued(node);
                        }
                        GetOrCreateSemaphore(node).Release();
                    })
                );
            }
        }

        private async void OnSpeakerCreated(Node node, Speaker speaker)
        {
            await GetOrCreateSemaphore(node).WaitAsync();
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
                speaker.Stopped += OnSpeakerStopped;
                _speakers.Add(node, speaker);
            }

            speaker.Start();
            SendEnqueued(node);
            GetOrCreateSemaphore(node).Release();
        }

        private void OnMessageReceivedFromSpeakerOrShouter(Node node, Message message)
        {
            if (node.ID != message.SenderID)
                _registry.UpdateNodeID(node, message.SenderID);

            _logger.Log(Tag.Communication, $"Received message of type {message.GetType()} from {node.ToString()}");
            
            try
            {
                if(message is NetworkStateMessage networkStateMessage)
                    OnNetworkStateReceived(node, networkStateMessage);
                else
                    OnMessageReceived?.Invoke(node, message);
            }
            catch(Exception ex) {
                _logger.Error(Tag.Communication, $"Exception generated while handling {message.GetType()} from {node.ToString()}", ex);
            }
        }

        private void OnSpeakerStopped(Node remote)
        {
            bool isBoldSpeaker = false;
            lock(_speakers)
            {
                if (_speakers.ContainsKey(remote))
                {
                    isBoldSpeaker = _speakers[remote] is BoldSpeaker;
                    _speakers[remote].MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
                    _speakers[remote].Stopped -= OnSpeakerStopped;
                    _speakers.Remove(remote);
                    remote.NotifyDeath();
                    _logger.Log(Tag.Communication, $"Speaker to {remote} deleted");
                }
            }
        }

        public async Task Send(Node node, Message message, SendFailureStrategy sendFailureStrategy = SendFailureStrategy.Discard)
        {
            await Send(node, message, sendFailureStrategy, false);
        }

        private async Task Send(Node node, Message message, SendFailureStrategy sendFailureStrategy = SendFailureStrategy.Discard, bool ignoreSemaphore = false)
        {
            if (!ignoreSemaphore)
                await GetOrCreateSemaphore(node).WaitAsync();

            try
            {
                message.ReceiverID = node.ID;

                await _sendOrdering.OrderedExecute(message, async () => {
                    if (_speakers.ContainsKey(node))
                        await _speakers[node].Send(message);
                    else
                    {
                        _logger.Warning(Tag.Communication, $"Doesn't exist a speaker for node {node}, Message ({_sender},{message.TimeStamp})");
                        if (sendFailureStrategy == SendFailureStrategy.Reconnect)
                            EnqueueMessage(node, message);
                    }
                });
            }
            catch (Exception e)
            {
                _logger.Error(Tag.Communication, $"Exception during sent to {node}", e);
                throw;
            }
            finally
            {
                if(message != null && message.TimeStamp.HasValue)
                    _sendOrdering.Observe(message);
                if (!ignoreSemaphore)
                    GetOrCreateSemaphore(node).Release();
            }
        }

        private SemaphoreSlim GetOrCreateSemaphore(Node node)
        {
            lock(_reconnectionSemaphores)
            {
                if (!_reconnectionSemaphores.ContainsKey(node))
                    _reconnectionSemaphores.Add(node, new SemaphoreSlim(1, 1));
                return _reconnectionSemaphores[node];
            }
        }

        private void EnqueueMessage(Node node, Message message)
        {
            if (!_notSent.ContainsKey(node))
                _notSent.Add(node, new Queue<Message>());
            _notSent[node].Enqueue(message);
            _logger.Log(Tag.Communication, $"Equeued message {message}, will be sent as soon as the speaker is connected to {node}");
        }

        private void SendEnqueued(Node node)
        {
            if (_notSent.ContainsKey(node))
            {
                _logger.Log(Tag.Communication, $"Start to send messages ({_notSent[node].Count}) enqueued to {node}");
                _notSent[node].ForEach<Message>(message => Send(node, message, SendFailureStrategy.Reconnect, true).Wait());
            }
        }

        public async Task SendMulticast(Message message)
        {
            try
            {
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
            _speakers.Clear();
            _shouter.Start();
            _pingThread = new Thread(() => {
                while(_pingThread != null)
                {
                    Speaker[] aliveSpeakers;
                    lock(_speakers)
                    {
                        aliveSpeakers = _speakers.Values.ToArray();
                    }
                    aliveSpeakers.ForEach(speaker => {
                        speaker.Ping().Wait();
                    });
                    Thread.Sleep(10000);
                }
            });
            _pingThread.Start();
        }
        
        public void Stop() 
        {
            _pingThread = null;
            _shouter.OnMessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
            _listener.SpeakerCreated -= OnSpeakerCreated;

            _shouter.Stop();
            _listener.Stop();

            _speakers.ForEach(speakerIdPair => 
            {
                speakerIdPair.Value.Stop();
                speakerIdPair.Value.MessageReceived -= OnMessageReceivedFromSpeakerOrShouter;
            });
            _speakers.Clear();

            _logger.Warning(Tag.Communication, "Network manager closed, no further communication can be performed");
        }

    }
}