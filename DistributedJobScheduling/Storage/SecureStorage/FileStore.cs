using System.Text;
using System;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Serialization;

namespace DistributedJobScheduling.Storage.SecureStorage
{
    public class FileStore<T> : IStore<T>, IInitializable
    {
        private string _filePath;
        private ISerializer _serializer;

        public FileStore(string filepath, ISerializer serializer)
        {
            _filePath = filepath;
            _serializer = serializer;
        }

        public void Init()
        {
            if (!File.Exists(_filePath))
                File.Create(_filePath).Dispose();
        }

        [return: MaybeNull]
        public T Read()
        {
            byte[] content = File.ReadAllBytes(_filePath);
            return _serializer.Deserialize<T>(content);
        }

        public void Write(T item)
        {
            byte[] bytes = _serializer.Serialize(item);
            File.WriteAllBytes(_filePath, bytes);
        }
    }
}