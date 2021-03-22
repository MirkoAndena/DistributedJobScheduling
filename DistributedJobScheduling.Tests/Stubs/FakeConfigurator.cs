
using System.Collections.Generic;
using DistributedJobScheduling.Configuration;

namespace DistributedJobScheduling.Tests
{
    public class FakeConfigurator : DictConfigService
    {
        public FakeConfigurator(Dictionary<string, object> configurations) :
            base(configurations) { }
    }
}