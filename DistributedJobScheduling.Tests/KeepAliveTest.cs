using System;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Messaging;
using DistributedJobScheduling.LeaderElection.KeepAlive;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.VirtualSynchrony;
using Xunit;

namespace DistributedJobScheduling.DistributedStorage
{
    public class KeepAliveTest
    {
        IGroupViewManager _group;
        ILogger _logger;
        ITimeStamper _timeStamper;
        
        public KeepAliveTest()
        {

        }

        private void KillNode(Node node)
        {
            // TODO KILL!
        }

        [Fact]
        public void WorkerFail()
        {
            Node diedNode = null;
            bool someoneDies = false;
            CoordinatorKeepAlive keepAlive = new CoordinatorKeepAlive(_group, _logger, _timeStamper);
            keepAlive.NodesDied += nodes =>
            {
                Assert.Equal(nodes.Count, 1);
                Assert.Equal(diedNode, nodes[0]);
                someoneDies = true;
            };
            keepAlive.Init();
            keepAlive.Start();
            Task.Delay(TimeSpan.FromSeconds(6)).ContinueWith(t => KillNode(diedNode));
            Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(t => 
            {
                keepAlive.Stop();
                Assert.True(someoneDies);
            });
        }

        [Fact]
        public void CoordinatorFail()
        {
            Node coordinator = null;
            bool coordinatorDies = false;
            WorkersKeepAlive keepAlive = new WorkersKeepAlive(_group, _logger, _timeStamper);
            keepAlive.CoordinatorDied += () =>
            {
                coordinatorDies = true;
            };
            keepAlive.Init();
            keepAlive.Start();
            Task.Delay(TimeSpan.FromSeconds(6)).ContinueWith(t => KillNode(coordinator));
            Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(t => 
            {
                keepAlive.Stop();
                Assert.True(coordinatorDies);
            });
        }
    }
}