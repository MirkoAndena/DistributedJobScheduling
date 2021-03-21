using System.Collections.Generic;
using System.IO;
using System.Text;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Storage.SecureStorage;

namespace DistributedJobScheduling.Tests
{
    public class MemoryStore<T> : IStore<T>
    {
        private T _storage;

        public MemoryStore()
        {
            
        }

        public T Read() => _storage;

        public void Write(T item) => _storage = item;
    }
}