using System;
using System.Collections.Generic;
using System.IO;

namespace DistributedJobScheduling.DistributedStorage
{
    public class Jobs
    { 
        public List<Job> value;

        public Jobs() { }
    }

    public class SecureStorage
    {
        private string FILENAME = "secure_storage.json";
        private static SecureStorage _instance;
        private List<Job> _value;

        public Action ValueChanged;

        public static SecureStorage Instance => _instance ??= new SecureStorage();
        public List<Job> Value => _value;

        private SecureStorage()
        {
            _value = Read();
            ValueChanged += Write;
        }

        private List<Job> Read()
        {
            if (!File.Exists(FILENAME))
            {
                File.Create(FILENAME);
                return new List<Job>();
            }

            string content = File.ReadAllText(FILENAME);
            Jobs jobs = JsonSerialization.Deserialize<Jobs>(content);
            if (jobs != null && jobs.value != null) return jobs.value;
            return new List<Job>();
        }

        private void Write()
        {
            Jobs jobs = new Jobs() { value = _value };
            byte[] json = JsonSerialization.Serialize(jobs);
            File.WriteAllBytes(FILENAME, json);
        }

        public void Close()
        {
            _instance = null;
            _value = null;
            ValueChanged -= Write;
        }
    }
}