
using System.Linq;
using System;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using System.Collections.Generic;

namespace DistributedJobScheduling.Storage.SecureStorage
{
    // T must has the default constructor (without parameters)
    public class BlockingDictionarySecureStore<T, CK, CT> : SecureStore<T>
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

        protected override void Write()
        {
            lock(_value)
            {
                base.Write();
            }
        }
    }
}