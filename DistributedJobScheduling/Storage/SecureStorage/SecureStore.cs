using System;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Storage.SecureStorage
{
    // T must has the default constructor (without parameters)
    public class SecureStore<T> : IInitializable
    {
        private IStore<T> _store;
        private ILogger _logger;

        public Action ValuesChanged;

        public T Value { get; set; }

        public SecureStore(IStore<T> store) : this(store, DependencyManager.Get<ILogger>()) { }
        public SecureStore(IStore<T> store, ILogger logger)
        {
            _logger = logger;
            _store = store;
            ValuesChanged += Write;
        }

        public void Init()
        {
            T stored = _store.Read();
            _logger.Log(Tag.SecureStorage, $"Read {typeof(T)} from secure storage");
            
            if (stored != null)
                Value = stored;
            else
            {
                _logger.Log(Tag.SecureStorage, $"No data read from secure storage");
                Value = Activator.CreateInstance<T>();
            }
        }

        private void Write()
        {
            _store.Write(Value);
            _logger.Log(Tag.SecureStorage, $"Writed {typeof(T)} to secure storage");
        }
    }
}