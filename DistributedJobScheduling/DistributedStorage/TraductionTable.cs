using System.Collections.Generic;
using DistributedJobScheduling.DistributedStorage.SecureStorage;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.DistributedStorage
{
    public class Table
    {
        public Dictionary<int, Job> Dictionary; 

        public Table() { Dictionary = new Dictionary<int, Job>(); }
    }

    public class TraductionTable
    {
        private int _jobIdCount = 0;
        private SecureStore<Table> _secureStorage;

        public TraductionTable() { _secureStorage = new SecureStore<Table>(); }
        public TraductionTable(IStore store)
        {
            _secureStorage = new SecureStore<Table>(store);
        }

        public int Add(Job job)
        {
            int id = _jobIdCount;
            _secureStorage.Value.Dictionary.Add(id, job);
            _secureStorage.ValuesChanged.Invoke();
            _jobIdCount++;
            return id;
        }
        public Job Get(int localID) => _secureStorage.Value.Dictionary[localID];
    }
}