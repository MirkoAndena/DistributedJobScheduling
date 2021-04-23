using System.Threading;
using System;

namespace DistributedJobScheduling.Storage
{
    public class ReusableIndex
    {
        const int BOUND = Int32.MaxValue;
        private int _index;
        private Predicate<int> _isIndexCurrentlyUsed;

        public ReusableIndex(Predicate<int> isIndexCurrentlyUsed)
        {
            _isIndexCurrentlyUsed = isIndexCurrentlyUsed;
        }

        public ReusableIndex()
        {
            _isIndexCurrentlyUsed = null;
        }

        public int NewIndex => FindNewIndex();

        private int FindNewIndex()
        {
            if (_isIndexCurrentlyUsed != null)
            {
               while (_isIndexCurrentlyUsed.Invoke(_index))
                    Interlocked.Increment(ref _index);
                return _index;
            }
            else
            {
                Interlocked.Increment(ref _index);
                return _index;
            }
        }
    }
}