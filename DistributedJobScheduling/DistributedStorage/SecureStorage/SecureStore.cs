using System;
using System.Collections.Generic;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.DistributedStorage.SecureStorage
{
    public class Jobs
    { 
        public List<Job> value;

        public Jobs() { }
    }

    public class SecureStore
    {
        private List<Job> _value;
        private IStore _store;

        public Action ValuesChanged;

        public List<Job> Values => _value;

        public SecureStore() : this(DependencyManager.Get<Storage>()) { }
        public SecureStore(IStore store)
        {
            _store = store;
            _value = Read();
            ValuesChanged += Write;
        }

        private List<Job> Read()
        {
            string stored = _store.Read(Stores.DistributedJobList);
            Jobs jobs = JsonSerialization.Deserialize<Jobs>(stored);
            if (jobs != null && jobs.value != null) return jobs.value;
            return new List<Job>();
        }

        private void Write()
        {
            Jobs jobs = new Jobs() { value = _value };
            byte[] json = JsonSerialization.Serialize(jobs);
            _store.Write(Stores.DistributedJobList, json);
        }

        public void Close()
        {
            _value = null;
            ValuesChanged -= Write;
        }
    }
}