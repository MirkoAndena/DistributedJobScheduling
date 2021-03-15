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

    public class TranslationTable : IMemoryCleaner
    {
        private ReusableIndex _reusableIndex;
        private SecureStore<Table> _secureStorage;

        public TranslationTable() { _secureStorage = new SecureStore<Table>(); }
        public TranslationTable(IStore store)
        {
            _secureStorage = new SecureStore<Table>(store);
            _reusableIndex = new ReusableIndex(index => _secureStorage.Value.Dictionary.ContainsKey(index));
        }

        public int Add(Job job)
        {
            int id = _reusableIndex.NewIndex;
            _secureStorage.Value.Dictionary.Add(id, new TableItem(job));
            _secureStorage.ValuesChanged.Invoke();
            return id;
        }

        public Job Get(int localID) => _secureStorage.Value.Dictionary[localID].Job;

        public void SetConfirmed(int localID)
        {
            _secureStorage.Value.Dictionary[localID].Confirmed = true;
            _secureStorage.ValuesChanged.Invoke();
        }

        public void SetJobID(int localID, int remoteID)
        {
            if (_secureStorage.Value.Dictionary.ContainsKey(localID))
            {
                _secureStorage.Value.Dictionary[localID].Job.ID = remoteID;
                _secureStorage.ValuesChanged.Invoke();
            }
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