
using System.Linq;
using System;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Logging;
using System.Collections.Generic;

namespace DistributedJobScheduling.Storage.SecureStorage
{
    // T must has the default constructor (without parameters)
    public class BlockingListSecureStore<T, CT> : SecureStore<T>
    where T : List<CT>
    {
        public BlockingListSecureStore(IStore<T> store) : this(store, DependencyManager.Get<ILogger>()) { }
        public BlockingListSecureStore(IStore<T> store, ILogger logger) : base(store, logger)
        {
        }

        public CT this[int index] => Get(index);

        public CT this[Predicate<CT> getter] => Get(getter);

        public CT Get(int index)
        {
            lock(_value)
            {
                return this._value[index];
            }
        }

        public CT Get(Predicate<CT> getter)
        {
            lock(_value)
            {
                foreach(CT item in _value)
                    if(getter(item)) return item;
                return default;
            }
        }

        public void ForEach(Action<CT> action)
        {
            lock(_value)
            {
                _value.ForEach(action);
            }
        }

        public List<CT> Clone()
        {
            lock(_value)
            {
                return new List<CT>(_value);
            }
        }

        public void Clear()
        {
            lock(_value)
            {
                _value.Clear();
            }
        }

        public void AddRange(IEnumerable<CT> elements)
        {
            lock(_value)
            {
                _value.AddRange(elements);
            }
        }

        public void Add(CT item)
        {
            lock(_value)
            {
                this._value.Add(item);
            }
        }

        public void RemoveAll(Predicate<CT> removePredicate)
        {
            lock(_value)
            {
                _value.RemoveAll(removePredicate);
            }
        }

        public void Remove(CT item)
        {
            lock(_value)
            {
                this._value.Remove(item);
            }
        }

        public bool Contains(CT item)
        {
            lock(_value)
            {
                return this._value.Contains(item);
            }
        }

        protected override void Write()
        {
            lock(_value)
            {
                base.Write();
            }
        }

        public void ExecuteTransaction(Action<List<CT>> transaction)
        {
            lock(_value)
            {
                transaction(_value);
            }
        }
    }
}