using System.Text;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
namespace DistributedJobScheduling.Serialization
{
    public class ByteBase64Serializer : ISerializer
    {
        public T Deserialize<T>(byte[] serialized)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            string base64 = Encoding.ASCII.GetString(serialized);
            using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(base64)))
            {
                object o = formatter.Deserialize(stream);
                return (T)o;
            }
        }

        public byte[] Serialize(object o)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, o);
                byte[] bytes = stream.ToArray();
                string base64 = Convert.ToBase64String(bytes);
                return Encoding.ASCII.GetBytes(base64);
            }
        }
    }
}