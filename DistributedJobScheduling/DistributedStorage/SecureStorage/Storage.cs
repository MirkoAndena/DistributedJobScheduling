using System.IO;
using DistributedJobScheduling.LifeCycle;

namespace DistributedJobScheduling.DistributedStorage.SecureStorage
{
    public class Storage : IStore, ILifeCycle
    {
        public void Init()
        {
            IStore.FilePaths.ForEach(path => 
            {
                if (!File.Exists(path.Value))
                    File.Create(path.Value);
            });
        }

        public string Read(Stores store)
        {
            string path = IStore.FilePaths[store];
            return File.ReadAllText(path);
        }

        public void Start()
        {

        }

        public void Stop()
        {
            
        }

        public void Write(Stores store, byte[] data)
        {
            string path = IStore.FilePaths[store];
            File.WriteAllBytes(path, data);
        }
    }
}