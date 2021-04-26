using System.Collections;
using System.Threading;
using System;
using System.Collections.Generic;

namespace DistributedJobScheduling.Storage
{
    public interface IContainer<K>
    {
        bool ContainsKey(K key);
    }

    public class ReusableIndex
    {
        const int BOUND = Int32.MaxValue;
        private int _index;
        private IContainer<int> _collection;

        public ReusableIndex(IContainer<int> collection)
        {
            _collection = collection;
        }

        public ReusableIndex()
        {
            _collection = null;
        }

        public int NewIndex => FindNewIndex();

        private int FindNewIndex()
        {
            if (_collection != null)
            {
                lock(_collection)
                {
                    while (_collection.ContainsKey(_index))
                        Interlocked.Increment(ref _index);
                    return _index;
                }
            }
            else
            {
                Interlocked.Increment(ref _index);
                return _index;
            }
        }
    }
}