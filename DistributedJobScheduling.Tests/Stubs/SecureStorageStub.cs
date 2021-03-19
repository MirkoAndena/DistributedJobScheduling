using System.Collections.Generic;
using System.IO;
using System.Text;
using DistributedJobScheduling.DistributedStorage.SecureStorage;
using DistributedJobScheduling.Extensions;

namespace DistributedJobScheduling.DistributedStorage
{
    public class SecureStorageStub : IStore
    {
        private Dictionary<string, string> _storage;

        public SecureStorageStub()
        {
            _storage = new Dictionary<string, string>();
            IStore.FilePaths.Values.ForEach(path => _storage.Add(path, string.Empty));
        }

        public string Read(Stores store)
        {
            return _storage[IStore.FilePaths[store]];
        }

        public void Write(Stores store, byte[] data)
        {
            _storage[IStore.FilePaths[store]] = Encoding.UTF8.GetString(data);
        }
    }
}