using System;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.DistributedStorage.SecureStorage
{
    // T must has the default constructor (without parameters)
    public class SecureStore<T>
    {
        private T _value;
        private IStore _store;
        private ILogger _logger;

        public Action ValuesChanged;

        public T Value => _value;

        public SecureStore() : this(DependencyManager.Get<Storage>(),
                                    DependencyManager.Get<ILogger>()) { }
        public SecureStore(IStore store, ILogger logger)
        {
            _logger = logger;
            _store = store;
            ValuesChanged += Write;
        }

        private T Read()
        {
            string stored = _store.Read(Stores.DistributedJobList);
            if (stored != string.Empty)
            {
                _logger.Log(Tag.SecureStorage, $"Read {typeof(T)} from secure storage");
                return JsonSerialization.Deserialize<T>(stored);
            } 
            _logger.Log(Tag.SecureStorage, $"No data read from secure storage");
            return Activator.CreateInstance<T>();
        }

        private void Write()
        {
            byte[] json = JsonSerialization.Serialize(_value);
            _store.Write(Stores.DistributedJobList, json);
            _logger.Log(Tag.SecureStorage, $"Writed {typeof(T)} to secure storage");
        }

        public void Init()
        {
            _value = Read();
        }

        public void Stop()
        {
            _logger.Warning(Tag.SecureStorage, "Secure storage closed");
            ValuesChanged -= Write;
        }
    }
}