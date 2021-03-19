using System.Text;
using System;
using System.IO;
using System.Diagnostics.CodeAnalysis;

namespace DistributedJobScheduling.Storage.SecureStorage
{
    public class JsonStore<T> : IStore<T>
    {
        private string _filePath;

        public JsonStore(string filepath)
        {
            _filePath = filepath;
            CreateEmptyNotExists();
        }

        private void CreateEmptyNotExists()
        {
            if (!File.Exists(_filePath))
                File.Create(_filePath);
        }

        [return: MaybeNull]
        public T Read()
        {
            string content = File.ReadAllText(_filePath);
            return JsonSerialization.Deserialize<T>(content);
        }

        public void Write(T item)
        {
            byte[] bytes = JsonSerialization.Serialize(item);
            File.WriteAllBytes(_filePath, bytes);
        }
    }
}