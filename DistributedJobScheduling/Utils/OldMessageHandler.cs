using System.Reflection.Metadata;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;
using DistributedJobScheduling.LifeCycle;

namespace DistributedJobScheduling
{   
    public class OldMessageHandler : IInitializable
    {
        private IGroupViewManager _groupManager;
        private ILogger _logger;
        private List<(Message, Node)> _notDeliveredMessages;

        public OldMessageHandler() : this(
            DependencyInjection.DependencyManager.Get<IGroupViewManager>(),
            DependencyInjection.DependencyManager.Get<ILogger>()
        ) {}

        public OldMessageHandler(IGroupViewManager groupViewManager, ILogger logger)
        {
            _groupManager = groupViewManager;
            _logger = logger;
            _notDeliveredMessages = new List<(Message, Node)>();
        }

        public void Init()
        {
            _groupManager.View.ViewChanged += OnViewChanged;
        }

        private void OnViewChanged()
        {
            // Send old messages when a stable view occured
            bool coordinatorAlive = _groupManager?.View?.Coordinator != null;
            if (coordinatorAlive)
            {
                _logger.Log(Tag.OldMessageHandler, $"Start to send messages ({_notDeliveredMessages.Count})");
                SendOldMessages();
                _logger.Log(Tag.OldMessageHandler, $"Sent {_notDeliveredMessages.Count} old messages");
            }
        }
        
        private void SendOldMessages()
        {
            _notDeliveredMessages.ForEach(pair => 
            {
                try 
                { 
                    Node node = pair.Item2;
                    Message message = pair.Item1;
                    if (node == null) _groupManager.SendMulticast(message).Wait();
                    else _groupManager.Send(node, message).Wait(); 
                    _notDeliveredMessages.Remove(pair);
                }
                catch (NotDeliveredException) { }
                catch (MulticastNotDeliveredException) { }
            });
        }

        public bool SendOrKeep(Node node, Message message)
        {
            try 
            { 
                _groupManager.Send(node, message).Wait();
                return true;
            }
            catch (NotDeliveredException) 
            { 
                _notDeliveredMessages.Add((message, node)); 
                _logger.Warning(Tag.OldMessageHandler, $"Message {message.TimeStamp.Value} to {node.ToString()} was not sent, is added to Queue");
                return false;
            }
        }

        public bool SendMulticastOrKeep(Message message)
        {
            try 
            { 
                _groupManager.SendMulticast(message).Wait();
                return true;
            }
            catch (MulticastNotDeliveredException) 
            { 
                _notDeliveredMessages.Add((message, null)); 
                _logger.Warning(Tag.OldMessageHandler, $"Message {message.TimeStamp.Value} to multicast was not sent, is added to Queue");
                return false;
            }
        }
    }
}