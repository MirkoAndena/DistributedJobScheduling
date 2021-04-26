
using System.Linq;
using System;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using System.Collections.Generic;
using DistributedJobScheduling.Extensions;

namespace DistributedJobScheduling.Storage.SecureStorage
{
    // T must has the default constructor (without parameters)
    public class BlockingDictionarySecureStore<T, CK, CT> : SecureStore<T>, IContainer<CK>
    where T : IDictionary<CK,CT>
    where CK: notnull
    {
        public BlockingDictionarySecureStore(IStore<T> store) : this(store, DependencyManager.Get<ILogger>()) { }
        public BlockingDictionarySecureStore(IStore<T> store, ILogger logger) : base(store, logger)
        {
            
        }

        public CT this[CK key]
        {
            get { return Get(key); }
            set { Update(key, value); }
        }

        public CT Get(CK index)
        {
            lock(_value)
            {
                return this._value[index];
            }
        }

        public List<CT> GetAll(Predicate<CT> predicate)
        {
            lock(_value)
            {
                List<CT> selected = new List<CT>();
                this._value.ForEach(item => {
                    if (predicate(item.Value))
                        selected.Add(item.Value);
                });
                return selected;
            }
        }

        public int Count(Predicate<CT> predicate)
        {
            lock(_value)
            {
                return this._value.Count(item => predicate(item.Value));
            }
        }

        public ICollection<CT> Values
        {
            get
            {
                return _value.Values;
            }
        }

        public ICollection<CK> Keys
        {
            get
            {
                return _value.Keys;
            }
        }

        public void Update(CK key, CT value)
        {
            lock(_value)
            {
                _value[key] = value;
            }
        }

        public void Add(CK key, CT item)
        {
            lock(_value)
            {
                this._value.Add(key, item);
            }
        }

        public void RemoveAll(Predicate<CT> removePredicate)
        {
            lock(_value)
            {
                foreach(CK key in _value.Keys.ToArray())
                {
                    if(removePredicate(_value[key]))
                        _value.Remove(key);
                }
            }
        }

        public void Remove(CK key)
        {
            lock(_value)
            {
                this._value.Remove(key);
            }
        }

        public bool ContainsKey(CK key)
        {
            lock(_value)
            {
                return this._value.ContainsKey(key);
            }
        }

        public void Clear()
        {
            lock(_value)
            {
                this._value.Clear();
            }
        }

        public void ExecuteTransaction(Action<T> transaction)
        {
            lock(_value)
            {
                transaction(_value);
            }
        }

        protected override void Write()
        {
            lock(_value)
            {
                base.Write();
            }
        }
    }
}