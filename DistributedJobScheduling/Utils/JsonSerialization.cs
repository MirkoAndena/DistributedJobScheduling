using System.Text;
using Newtonsoft.Json;

namespace DistributedJobScheduling
{
    public static class JsonSerialization
    {
        public static byte[] Serialize(object o)
        {
            string json = JsonConvert.SerializeObject(o);
            return Encoding.UTF8.GetBytes(json);
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json);
        }
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}