using System.Text;
using Newtonsoft.Json;

namespace DistributedJobScheduling
{
    public static class JsonSerialization
    {
        private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public static byte[] Serialize(object o)
        {
            string json = JsonConvert.SerializeObject(o, jsonSettings);
            return Encoding.UTF8.GetBytes(json);
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json, jsonSettings);
        }
        
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, jsonSettings);
        }
    }
}