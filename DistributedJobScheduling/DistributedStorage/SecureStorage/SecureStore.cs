using System;
using System.Collections.Generic;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.DistributedStorage.SecureStorage
{
    // T must has the default constructor (without parameters)
    public class SecureStore<T>
    {
        private T _value;
        private IStore _store;

        public Action ValuesChanged;

        public T Value => _value;

        public SecureStore() : this(DependencyManager.Get<Storage>()) { }
        public SecureStore(IStore store)
        {
            _store = store;
            _value = Read();
            ValuesChanged += Write;
        }

        private T Read()
        {
            string stored = _store.Read(Stores.DistributedJobList);
            if (stored != string.Empty) return JsonSerialization.Deserialize<T>(stored);
            return Activator.CreateInstance<T>();
        }

        private void Write()
        {
            byte[] json = JsonSerialization.Serialize(_value);
            _store.Write(Stores.DistributedJobList, json);
        }

        public void Close()
        {
            ValuesChanged -= Write;
        }
    }
}