using System.IO;

namespace DistributedJobScheduling.DistributedStorage.SecureStorage
{
    public class Storage : IStore
    {
        public string Read(Stores store)
        {
            string _filepath = IStore.FilePaths[store];
            if (!File.Exists(_filepath))
            {
                File.Create(_filepath);
                return string.Empty;
            }

            return File.ReadAllText(_filepath);
        }

        public void Write(Stores store, byte[] data)
        {
            string _filepath = IStore.FilePaths[store];
            File.WriteAllBytes(_filepath, data);
        }
    }
}