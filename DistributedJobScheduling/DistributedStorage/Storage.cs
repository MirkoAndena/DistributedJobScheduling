using System.IO;

namespace DistributedJobScheduling.DistributedStorage
{
    public class Storage : IStore
    {
        const string FILENAME = "secure_storage.json";
        public string Read()
        {
            if (!File.Exists(FILENAME))
            {
                File.Create(FILENAME);
                return string.Empty;
            }

            return File.ReadAllText(FILENAME);
        }

        public void Write(byte[] data)
        {
            throw new System.NotImplementedException();
        }
    }
}