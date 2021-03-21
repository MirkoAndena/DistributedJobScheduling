using System.Reflection.Metadata;
using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Storage.SecureStorage;

namespace DistributedJobScheduling.Storage
{
    public class TableItem
    {
        public Job Job;
        public bool Confirmed;

        public TableItem() { Confirmed = false; }
        public TableItem(Job job) : this() { Job = job; }
    }

    public class Table
    {
        // Key: requestID, Job, bool: confirmed by client
        public Dictionary<int, TableItem> Dictionary; 

        public Table() { Dictionary = new Dictionary<int, TableItem>(); }
    }

    public class TranslationTable : IInitializable
    {
        private ReusableIndex _reusableIndex;
        private SecureStore<Table> _secureStorage;
        private ILogger _logger;

        public TranslationTable() : this(
            DependencyInjection.DependencyManager.Get<IStore<Table>>(),
            DependencyInjection.DependencyManager.Get<ILogger>()) { }
        public TranslationTable(IStore<Table> store, ILogger logger)
        {
            _logger = logger;
            _secureStorage = new SecureStore<Table>(store, logger);
            _reusableIndex = new ReusableIndex(index => _secureStorage.Value.Dictionary.ContainsKey(index));
        }

        public void Init() => DeleteUnconfirmedEntries();

        public int Add(Job job)
        {
            int id = _reusableIndex.NewIndex;
            _secureStorage.Value.Dictionary.Add(id, new TableItem(job));
            _secureStorage.ValuesChanged.Invoke();
            _logger.Log(Tag.TranslationTable, $"Added job {job} with local id {id} (not confirmed)");
            return id;
        }

        public Job Get(int localID) => _secureStorage.Value.Dictionary[localID].Job;

        public void SetConfirmed(int localID)
        {
            _secureStorage.Value.Dictionary[localID].Confirmed = true;
            _secureStorage.ValuesChanged.Invoke();
            _logger.Log(Tag.TranslationTable, $"Entry with id {localID} is confirmed");
        }

        public void SetJobID(int localID, int remoteID)
        {
            if (_secureStorage.Value.Dictionary.ContainsKey(localID))
            {
                _secureStorage.Value.Dictionary[localID].Job.ID = remoteID;
                _secureStorage.ValuesChanged.Invoke();
                _logger.Log(Tag.TranslationTable, $"Setted job id ({remoteID}) from coordinator to entry with id {localID}");
            }
        }

        private void DeleteUnconfirmedEntries()
        {
            _secureStorage.Value.Dictionary.ForEach(idItemPair => 
            {
                if (!idItemPair.Value.Confirmed)
                    _secureStorage.Value.Dictionary.Remove(idItemPair.Key);
            });
            _secureStorage.ValuesChanged.Invoke();
            _logger.Log(Tag.TranslationTable, "Unconfirmed entries deleted");
        }
    }
}