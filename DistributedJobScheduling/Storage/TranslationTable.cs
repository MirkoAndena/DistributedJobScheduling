using System.Runtime.InteropServices;
using System.Reflection.Metadata;
using System.Collections.Generic;
using DistributedJobScheduling.JobAssignment.Jobs;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Extensions;
using DistributedJobScheduling.Storage.SecureStorage;

namespace DistributedJobScheduling.Storage
{
    public class TranslationTable : IInitializable, ITranslationTable
    {
        private ReusableIndex _reusableIndex;
        private BlockingDictionarySecureStore<Dictionary<int, int?>, int, int?> _secureStorage;
        private ILogger _logger;

        public TranslationTable() : this(
            DependencyInjection.DependencyManager.Get<IStore<Dictionary<int, int?>>>(),
            DependencyInjection.DependencyManager.Get<ILogger>())
        { }
        public TranslationTable(IStore<Dictionary<int, int?>> store, ILogger logger)
        {
            _logger = logger;
            _secureStorage = new BlockingDictionarySecureStore<Dictionary<int, int?>, int, int?>(store, logger);
            _reusableIndex = new ReusableIndex(index => _secureStorage.ContainsKey(index));
        }

        public void Init()
        {
            _secureStorage.Init();
            DeleteUnAssignedIDs();
        }

        private void DeleteUnAssignedIDs() => _secureStorage.RemoveAll(id => id == null);

        public int CreateNewIndex => _reusableIndex.NewIndex;

        public void StoreIndex(int requestId)
        {
            _secureStorage.Add(requestId, null);
            _secureStorage.ValuesChanged?.Invoke();
            _logger.Log(Tag.TranslationTable, $"Stored request id {requestId} with no job id");
        }

        public void Update(int requestId, int job)
        {
            if (!_secureStorage.ContainsKey(requestId))
                throw new System.Exception($"No entry found in translation table with id {requestId}, you have to call StoreIndex");

            _secureStorage[requestId] = job;
            _secureStorage.ValuesChanged?.Invoke();
            _logger.Log(Tag.TranslationTable, $"Added job {job} with local id {requestId}");
        }

        public int? Get(int localID)
        {
            if (_secureStorage.ContainsKey(localID))
                return _secureStorage[localID];
            return null;
        }

        public void Remove(int localID)
        {
            if (_secureStorage.ContainsKey(localID))
                _secureStorage.Remove(localID);
        }
    }
}