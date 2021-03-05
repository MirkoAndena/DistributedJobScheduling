using System.Text;
using Newtonsoft.Json;

namespace DistributedJobScheduling.Communication.Basic
{
    public abstract class Message
    {
        public byte[] Serialize()
        {
            string json = JsonConvert.SerializeObject(this);
            return Encoding.UTF8.GetBytes(json);
        }

        public static T Deserialize<T>(byte[] bytes) where T: Message
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}