using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
namespace DistributedJobScheduling.Serialization
{
    public class ByteSerializer : ISerializer
    {
        private BinaryFormatter _binaryFormatter;

        public ByteSerializer()
        {
            _binaryFormatter = new BinaryFormatter();
        }

        public T Deserialize<T>(byte[] serialized)
        {
            using (MemoryStream stream = new MemoryStream(serialized))
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
                return stream.ToArray();
            }
        }
    }
}