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

        public FileStore(string filepath) : this (filepath, DependencyInjection.DependencyManager.Get<ISerializer>()) { }
        
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
            if(content.Length > 0)
                return _serializer.Deserialize<T>(content);
            return default;
        }

        public void Write(T item)
        {
            byte[] bytes = _serializer.Serialize(item);
            File.WriteAllBytes(_filePath, bytes);
        }
    }
}