using System;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Storage.SecureStorage
{
    // T must has the default constructor (without parameters)
    public abstract class SecureStore<T> : IInitializable
    {
        protected IStore<T> _store;
        protected ILogger _logger;
        protected T _value;

        public Action ValuesChanged;

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
            
            if (stored != null)
            {
                _logger.Log(Tag.SecureStorage, $"Read {typeof(T).Name} from secure storage");
                _value = stored;
            }
            else
            {
                _logger.Log(Tag.SecureStorage, $"No data read from secure storage");
                _value = Activator.CreateInstance<T>();
            }
        }

        protected virtual void Write()
        {
            _store.Write(_value);
            _logger.Log(Tag.SecureStorage, $"Wrote {typeof(T)} to secure storage");
        }
    }
}