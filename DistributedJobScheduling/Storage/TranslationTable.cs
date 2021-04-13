using System.Reflection.Metadata;
using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Storage.SecureStorage;

namespace DistributedJobScheduling.Storage
{
    public class Table
    {
        public Dictionary<int, int> Dictionary; 

        public Table() { Dictionary = new Dictionary<int, int>(); }
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

        public void Init()
        {
            _secureStorage.Init();
        }

        public int CreateNewIndex => _reusableIndex.NewIndex;

        public void StoreIndex(int requestId)
        {
            _secureStorage.Value.Dictionary.Add(requestId, -1);
            _secureStorage.ValuesChanged?.Invoke();
            _logger.Log(Tag.TranslationTable, $"Stored request id {requestId} with no job id");
        }

        public void Update(int requestId, int job)
        {
            if (!_secureStorage.Value.Dictionary.ContainsKey(requestId))
                throw new System.Exception($"No entry found in translation table with id {requestId}, you have to call StoreIndex");

            _secureStorage.Value.Dictionary[requestId] = job;
            _secureStorage.ValuesChanged?.Invoke();
            _logger.Log(Tag.TranslationTable, $"Added job {job} with local id {requestId}");
        }

        public int? Get(int localID) 
        {
            if (_secureStorage.Value.Dictionary.ContainsKey(localID))
                return _secureStorage.Value.Dictionary[localID];
            return null;
        }
    }
}