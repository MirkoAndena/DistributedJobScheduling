using System.Text;
using System;
using System.Collections.Generic;
using DistributedJobScheduling.Extensions;

namespace DistributedJobScheduling.Configuration
{
    public class DictConfigService : IConfigurationService
    {
        private Dictionary<string, object> _values;

        public DictConfigService()
        {
            _values = new Dictionary<string, object>();
        }

        protected DictConfigService(Dictionary<string, object> values)
        {
            _values = values;
        }

        public void SetValue<T>(string key, T value) => _values.Add(key, value);

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (!_values.ContainsKey(key))
                return defaultValue;

            if (_values[key] is T)
                return (T)_values[key];
                
            throw new Exception($"{key} is not of type {typeof(T)}");
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            _values.ForEach(value => stringBuilder.Append($"{value.Key}: {value.Value.ToString()}, "));
            return stringBuilder.ToString().Trim(new char[] { ' ', ','});
        }
    }
}