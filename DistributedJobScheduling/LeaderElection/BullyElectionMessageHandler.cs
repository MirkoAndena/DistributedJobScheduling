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
    public class BullyElectionMessageHandler : IInitializable
    {
        private ILogger _logger;
        private BullyElectionCandidate _candidate;
        private IGroupViewManager _groupManager;
        private bool _electionInProgress;

        public BullyElectionMessageHandler() : this (DependencyInjection.DependencyManager.Get<ILogger>(),
                                                    DependencyInjection.DependencyManager.Get<IGroupViewManager>()) {}
        public BullyElectionMessageHandler(ILogger logger, IGroupViewManager groupViewManager)
        {
            _logger = logger;
            _groupManager = groupViewManager;
            _candidate = new BullyElectionCandidate(groupViewManager, logger);
        }

        public void Init()
        {
            _groupManager.View.ViewChanged += OnViewChanged;
            _groupManager.ViewChanging += Stop;
        }

        private void Start()
        {
            var jobPublisher = _groupManager.Topics.GetPublisher<BullyElectionPublisher>();
            jobPublisher.RegisterForMessage(typeof(ElectMessage), OnElectMessageArrived);
            jobPublisher.RegisterForMessage(typeof(CoordMessage), OnCoordMessageArrived);
            jobPublisher.RegisterForMessage(typeof(CancelMessage), OnCancelMessageArrived);
            _candidate.SendElect += SendElectMessages;
            _candidate.SendCoords += SendCoordMessages;
        }

        private void Stop()
        {
            var jobPublisher = _groupManager.Topics.GetPublisher<BullyElectionPublisher>();
            jobPublisher.UnregisterForMessage(typeof(ElectMessage), OnElectMessageArrived);
            jobPublisher.UnregisterForMessage(typeof(CoordMessage), OnCoordMessageArrived);
            jobPublisher.UnregisterForMessage(typeof(CancelMessage), OnCancelMessageArrived);
            _candidate.CancelElection();
            _candidate.SendElect -= SendElectMessages;
            _candidate.SendCoords -= SendCoordMessages;
        }

        private void OnViewChanged()
        {
            if (_groupManager.View.Coordinator == null)
            {
                _electionInProgress = false;
                Start();
                OnCoordinatorDeathReported();
            }
        }

        private void OnCoordinatorDeathReported()
        {
            _logger.Log(Tag.LeaderElection, $"Coordinator is dead, starting election");
            _candidate.Run();
        }

        private void SendElectMessages(List<Node> nodes)
        {
            _electionInProgress = true;
            nodes.ForEach(node => 
            {
                _groupManager.Send(node, new ElectMessage(_groupManager.View.Me.ID.Value)).Wait();
            });
        }

        private void SendCoordMessages(List<Node> nodes)
        {
            _electionInProgress = false;
            nodes.ForEach(node => 
            {
                _groupManager.Send(node, new CoordMessage(_groupManager.View.Me)).Wait();
            });
            _groupManager.View.UpdateCoordinator(_groupManager.View.Me);
        }

        private void OnElectMessageArrived(Node node, Message message)
        {
            ElectMessage arrived = (ElectMessage)message;
            int myID = _groupManager.View.Me.ID.Value;
            if (myID > arrived.ID)
            {
                _logger.Log(Tag.LeaderElection, $"Received ELECT from {node.ID.Value}, my id ({myID}) is greater so i start a new election");
                _groupManager.Send(node, new CancelMessage());
                
                if (!_electionInProgress)
                    _candidate.Run();
            }
            else
            {
                _candidate.CancelElection();
                _logger.Log(Tag.LeaderElection, $"Received ELECT from {node.ID.Value}, OK");
            }
                
            _electionInProgress = true;
        }

        private void OnCoordMessageArrived(Node node, Message message)
        {
            CoordMessage arrived = (CoordMessage)message;
            _electionInProgress = false;
            _groupManager.View.UpdateCoordinator(arrived.Coordinator);
            _logger.Log(Tag.LeaderElection, $"Received COORD from {node.ID.Value}, updated");
            Stop();
        }

        private void OnCancelMessageArrived(Node node, Message message)
        {
            _logger.Log(Tag.LeaderElection, "Election cancelled");
            _candidate.CancelElection();
        }
    }
}