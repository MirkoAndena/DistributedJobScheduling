using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.Communication.Messaging.LeaderElection;
using DistributedJobScheduling.LeaderElection.KeepAlive;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;

namespace DistributedJobScheduling.LeaderElection
{
    public class BullyElectionMessageHandler : IInitializable, IStartable
    {
        private ILogger _logger;
        private BullyElectionCandidate _candidate;
        private IGroupViewManager _groupManager;
        private ITimeStamper _timeStamper;

        public BullyElectionMessageHandler() : this (DependencyInjection.DependencyManager.Get<ILogger>(),
                                                    DependencyInjection.DependencyManager.Get<ITimeStamper>(),
                                                    DependencyInjection.DependencyManager.Get<IGroupViewManager>()) {}
        public BullyElectionMessageHandler(ILogger logger, ITimeStamper timeStamper, IGroupViewManager groupViewManager)
        {
            _logger = logger;
            _timeStamper = timeStamper;
            _groupManager = groupViewManager;
            _candidate = new BullyElectionCandidate(groupViewManager, logger);
        }

        public void Init()
        {
            var jobPublisher = _groupManager.Topics.GetPublisher<BullyElectionPublisher>();
            jobPublisher.RegisterForMessage(typeof(ElectMessage), OnElectMessageArrived);
            jobPublisher.RegisterForMessage(typeof(CoordMessage), OnCoordMessageArrived);
        }

        public void Start()
        {
            _candidate.SendElect += SendElectMessages;
            _candidate.SendCoords += SendCoordMessages;
            _groupManager.View.ViewChanged += OnViewChanged;
        }

        public void Stop()
        {
            _candidate.CancelElection();
            _candidate.SendElect -= SendElectMessages;
            _candidate.SendCoords -= SendCoordMessages;
            _groupManager.View.ViewChanged -= OnViewChanged;
        }

        private void OnViewChanged()
        {
            if (_groupManager.View.Coordinator == null)
                OnCoordinatorDeathReported();
        }

        private void OnCoordinatorDeathReported()
        {
            Node coordinator = _groupManager.View.Coordinator;
            _logger.Log(Tag.LeaderElection, $"Coordinator {coordinator.ID.Value} is dead, starting election");
            _candidate.Run(coordinator);
        }

        private void SendElectMessages(List<Node> nodes)
        {
            nodes.ForEach(node => 
            {
                _groupManager.Send(node, new ElectMessage(_groupManager.View.Me.ID.Value, _timeStamper)).Wait();
            });
        }

        private void SendCoordMessages(List<Node> nodes)
        {
            nodes.ForEach(node => 
            {
                _groupManager.Send(node, new CoordMessage(_groupManager.View.Me, _timeStamper)).Wait();
            });
        }

        private void OnElectMessageArrived(Node node, Message message)
        {
            ElectMessage arrived = (ElectMessage)message;
            int myID = _groupManager.View.Me.ID.Value;
            _candidate.CancelElection();
            if (myID > arrived.ID)
            {
                _logger.Log(Tag.LeaderElection, $"Received ELECT from {node.ID.Value}, my id ({myID}) is greater so i start a new election");
                _candidate.Run();
            }
            else
                _logger.Log(Tag.LeaderElection, $"Received ELECT from {node.ID.Value}, OK");
        }

        private void OnCoordMessageArrived(Node node, Message message)
        {
            CoordMessage arrived = (CoordMessage)message;
            _groupManager.View.UpdateCoordinator(arrived.Coordinator);
            _logger.Log(Tag.LeaderElection, $"Received COORD from {node.ID.Value}, updated");
        }
    }
}