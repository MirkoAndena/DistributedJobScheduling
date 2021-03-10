
using System.Collections.Generic;
using DistributedJobScheduling.Configuration;

namespace DistributedJobScheduling.Tests
{
    public class FakeConfigurator : IConfigurationService
    {
        private Dictionary<string, object> _configurations;

        public FakeConfigurator(Dictionary<string, object> configurations)
        {
            _configurations = new Dictionary<string, object>(configurations);
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if(!_configurations.ContainsKey(key))
                return default;
            return (T)_configurations[key];
        }
    }
}