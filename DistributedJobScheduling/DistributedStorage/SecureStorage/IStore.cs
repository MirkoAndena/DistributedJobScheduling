using System.Collections.Generic;

namespace DistributedJobScheduling.DistributedStorage.SecureStorage
{
    public enum Stores
    {
        DistributedJobList,
        LocalJobTraduction
    }

    public interface IStore
    {
        protected static Dictionary<Stores, string> FilePaths = new Dictionary<Stores, string>()
        {
            [Stores.DistributedJobList] = "distributed_jobs_list.json",
            [Stores.LocalJobTraduction] = "local_jobs_traduction.json"
        };

        string Read(Stores store);
        void Write(Stores store, byte[] data);
    }
}