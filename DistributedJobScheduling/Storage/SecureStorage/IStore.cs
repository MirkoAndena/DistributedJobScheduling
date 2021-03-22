using System.Collections.Generic;

namespace DistributedJobScheduling.Storage.SecureStorage
{
    public interface IStore<T>
    {
        T Read();
        void Write(T item);
    }
}