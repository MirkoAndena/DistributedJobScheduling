using System;
using System.Collections.Generic;
using System.IO;
using DistributedJobScheduling.DependencyInjection;

namespace DistributedJobScheduling.DistributedStorage
{
    public class Jobs
    { 
        public List<Job> value;

        public Jobs() { }
    }

    public interface IStore
    {
        string Read();
        void Write(byte[] data);
    }

    public class SecureStorage
    {
        private List<Job> _value;
        private IStore _store;

        public Action ValuesChanged;

        public List<Job> Values => _value;

        public SecureStorage() : this(DependencyManager.Get<Storage>()) { }
        public SecureStorage(IStore store)
        {
            _store = store;
            _value = Read();
            ValuesChanged += Write;
        }

        private List<Job> Read()
        {
            string stored = _store.Read();
            Jobs jobs = JsonSerialization.Deserialize<Jobs>(stored);
            if (jobs != null && jobs.value != null) return jobs.value;
            return new List<Job>();
        }

        private void Write()
        {
            Jobs jobs = new Jobs() { value = _value };
            byte[] json = JsonSerialization.Serialize(jobs);
            _store.Write(json);
        }

        public void Close()
        {
            _value = null;
            ValuesChanged -= Write;
        }
    }
}