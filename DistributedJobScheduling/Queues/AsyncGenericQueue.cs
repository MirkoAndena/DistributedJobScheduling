
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DistributedJobScheduling.Queues
{
    /// <summary>
    /// Queue handling where there are multiple producers and one consumer
    /// with C# async
    /// </summary>
    public class AsyncGenericQueue<T>
    {
        private Queue<T> _queue;
        private TaskCompletionSource<bool> _dataAvailable;
        private bool _waitingForElement;

        public AsyncGenericQueue() : this(null) {}
        public AsyncGenericQueue(Queue<T> other)
        {
            _queue = new Queue<T>(other ?? new Queue<T>());
            _dataAvailable = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void EnqueueRange(IEnumerable<T> elements)
        {
            lock(_queue)
            {
                foreach(T element in elements)
                    _queue.Enqueue(element);
                _dataAvailable.TrySetResult(true);
            }
        }

        public void Enqueue(T element)
        {
            lock(_queue)
            {
                _queue.Enqueue(element);
                _dataAvailable.TrySetResult(true);
            }
        }

        /// <summary>
        /// Dequeues every element in the queue
        /// </summary>
        /// <returns></returns>
        public Queue<T> DequeueAll()
        {
            Queue<T> elements;

            lock(_queue)
            {
                elements = new Queue<T>(_queue);
                _queue.Clear();

                if(!_dataAvailable.Task.IsCompleted)
                    _dataAvailable.SetResult(false);
            }

            return elements;
        }

        /// <summary>
        /// Dequeues one element from the queue, synchronous if there are already elements in the queue
        /// Async if there are no elements in the queue
        /// </summary>
        /// <returns>Returns element if we can dequeue an element, default(T) if we can't dequeue at the moment</returns>
        //TODO: Cancellation token?
        public async Task<T> Dequeue(CancellationToken cancellationToken = default)
        {
            T element = default(T);
            lock(this)
            {
                if(_waitingForElement)
                    throw new System.Exception("Cannot have multiple consumers for this queue");
                _waitingForElement = true;
            }

            await _dataAvailable.Task;
            
            lock(_queue)
            {
                if(_queue.Count > 0) element = _queue.Dequeue();
                if(_queue.Count == 0)
                    _dataAvailable = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _waitingForElement = false;
            return element;
        }
    }
}