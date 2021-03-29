using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Communication.Basic.Speakers;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Serialization;
using static DistributedJobScheduling.Communication.Basic.Node;

namespace DistributedJobScheduling.Client
{
    public class JobMessageHandler
    {
        private BoldSpeaker _speaker;
        private ILogger _logger;

        public JobMessageHandler() : this (
            DependencyInjection.DependencyManager.Get<ILogger>()) { }

        public JobMessageHandler(ILogger logger)
        {
            _logger = logger;
        }

        
    }
}