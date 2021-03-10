using System.IO;
using System.Text;

namespace DistributedJobScheduling.DistributedStorage
{
    public class SecureStorageStub : IStore
    {
        private string _storage;

        public string Read()
        {
            return _storage;
        }

        public void Write(byte[] data)
        {
            _storage = Encoding.UTF8.GetString(data);
        }
    }
}