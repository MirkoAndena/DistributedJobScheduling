using System.Text;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
namespace DistributedJobScheduling.Serialization
{
    public class ByteBase64Serializer : ISerializer
    {
        private BinaryFormatter _binaryFormatter;

        public ByteBase64Serializer()
        {
            _binaryFormatter = new BinaryFormatter();
        }

        public T Deserialize<T>(byte[] serialized)
        {
            string base64 = Encoding.ASCII.GetString(serialized);
            using (MemoryStream stream = new MemoryStream(Convert.FromBase64String(base64)))
            {
                object o = _binaryFormatter.Deserialize(stream);
                return (T)o;
            }
        }

        public byte[] Serialize(object o)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                _binaryFormatter.Serialize(stream, o);
                byte[] bytes = stream.ToArray();
                string base64 = Convert.ToBase64String(bytes);
                return Encoding.ASCII.GetBytes(base64);
            }
        }
    }
}