using System.Reflection.Metadata;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.LifeCycle;
using System;

namespace DistributedJobScheduling
{   
    public struct NotDeliveredMessage
    {
        public Node Dest;
        public bool DestIsCoordintor;
        public bool Multicast;
        public Message Message;
        public Action Action;
    } 

    public class OldMessageHandler : IInitializable
    {
        private IGroupViewManager _groupManager;
        private ILogger _logger;
        private List<NotDeliveredMessage> _notDeliveredMessages;

        public OldMessageHandler() : this(
            DependencyInjection.DependencyManager.Get<IGroupViewManager>(),
            DependencyInjection.DependencyManager.Get<ILogger>()
        ) {}

        public OldMessageHandler(IGroupViewManager groupViewManager, ILogger logger)
        {
            _groupManager = groupViewManager;
            _logger = logger;
            _notDeliveredMessages = new List<NotDeliveredMessage>();
        }

        public void Init()
        {
            _groupManager.View.ViewChanged += OnViewChanged;
        }

        private void OnViewChanged()
        {
            // Send old messages when a stable view occured
            if (_groupManager.View.CoordinatorExists)
            {
                _logger.Log(Tag.OldMessageHandler, $"Start to send messages ({_notDeliveredMessages.Count})");
                SendOldMessages();
                _logger.Log(Tag.OldMessageHandler, $"Sent {_notDeliveredMessages.Count} old messages");
            }
        }
        
        private void SendOldMessages()
        {
            _notDeliveredMessages.ForEach(message => 
            {
                try 
                { 
                    if (message.Multicast)
                    {
                        _groupManager.SendMulticast(message.Message);
                    }
                    else
                    {
                        Node dest = message.DestIsCoordintor ? _groupManager.View.Coordinator : message.Dest;
                        _groupManager.Send(dest, message.Message);
                    }

                    message.Action?.Invoke();
                    _notDeliveredMessages.Remove(message);
                }
                catch (NotDeliveredException) { }
                catch (MulticastNotDeliveredException) { }
            });
        }

        public void SendOrKeep(Node node, Message message, Action action = null)
        {
            try 
            { 
                _groupManager.Send(node, message).Wait();
                _logger.Log(Tag.OldMessageHandler, $"Sent ({_groupManager.View.Me.ID},{message.TimeStamp}) message: {message.ToString()}");
                action?.Invoke();
            }
            catch (NotDeliveredException) 
            { 
                NotDeliveredMessage notDelivered = new NotDeliveredMessage();
                notDelivered.Dest = node;
                notDelivered.DestIsCoordintor = node == _groupManager.View.Coordinator;
                notDelivered.Multicast = false;
                notDelivered.Message = message;
                notDelivered.Action = action;

                _notDeliveredMessages.Add(notDelivered); 
                _logger.Warning(Tag.OldMessageHandler, $"Message {message.TimeStamp.Value} to {node.ToString()} was not sent, is added to Queue");
            }
        }

        public void SendMulticastOrKeep(Message message, Action action = null)
        {
            try 
            { 
                _groupManager.SendMulticast(message).Wait();
                _logger.Log(Tag.OldMessageHandler, $"Sent multicast ({_groupManager.View.Me.ID},{message.TimeStamp}) message: {message.ToString()}");
                action?.Invoke();
            }
            catch (MulticastNotDeliveredException) 
            { 
                NotDeliveredMessage notDelivered = new NotDeliveredMessage();
                notDelivered.Multicast = true;
                notDelivered.Message = message;
                notDelivered.Action = action;

                _notDeliveredMessages.Add(notDelivered); 
                _logger.Warning(Tag.OldMessageHandler, $"Message {message.TimeStamp.Value} to multicast was not sent, is added to Queue");
            }
        }
    }
}