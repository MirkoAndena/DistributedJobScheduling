using System.Text;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Serialization
{
    public class JsonSerializer : ISerializer
    {
        private JsonSerializerSettings _settings;

        public JsonSerializer()
        {
            _settings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.All,
                Formatting = Formatting.Indented
            };
        }

        public byte[] Serialize(object o)
        {
            string json = JsonConvert.SerializeObject(o, _settings);
            return Encoding.UTF8.GetBytes(json);
        }

        public T Deserialize<T>(byte[] bytes)
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }
    }
}