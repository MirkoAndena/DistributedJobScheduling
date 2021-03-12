using System.Collections.Generic;
using DistributedJobScheduling.DistributedStorage.SecureStorage;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.DistributedStorage
{
    class TableItem
    {
        public Job Job;
        public bool Confirmed;

        public TableItem() { Confirmed = false; }
        public TableItem(Job job) : this() { Job = job; }
    }

    class Table
    {
        // Key: requestID, Job, bool: confirmed by client
        public Dictionary<int, TableItem> Dictionary; 

        public Table() { Dictionary = new Dictionary<int, TableItem>(); }
    }

    public class TraductionTable : IMemoryCleaner
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
            _secureStorage.Value.Dictionary.Add(id, new TableItem(job));
            _secureStorage.ValuesChanged.Invoke();
            _jobIdCount++;
            return id;
        }

        public Job Get(int localID) => _secureStorage.Value.Dictionary[localID].Job;

        public void SetConfirmed(int localID)
        {
            _secureStorage.Value.Dictionary[localID].Confirmed = true;
            _secureStorage.ValuesChanged.Invoke();
        }

        public void CleanLogicRemoved() => DeleteUnconfirmedEntries();
        private void DeleteUnconfirmedEntries()
        {
            _secureStorage.Value.Dictionary.ForEach((id, tableitem) => 
            {
                if (!tableitem.Confirmed)
                    _secureStorage.Value.Dictionary.Remove(id);
            });
            _secureStorage.ValuesChanged.Invoke();
        }
    }
}