using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging.Ordering
{
    public class FIFOMessageOrdering : IMessageOrdering
    {
        private int? _lastObserved = null;
        private Dictionary<int, TaskCompletionSource<bool>> _waitingQueue;

        public FIFOMessageOrdering()
        {
            _waitingQueue = new Dictionary<int, TaskCompletionSource<bool>>();
        }

        public async Task EnsureOrdering(Message message)
        {
            TaskCompletionSource<bool> waitSendTask;
            lock(_waitingQueue)
            {
                if(!_waitingQueue.ContainsKey(message.TimeStamp))
                    _waitingQueue.Add(message.TimeStamp, new TaskCompletionSource<bool>());
                waitSendTask = _waitingQueue[message.TimeStamp];
            }

            if(_lastObserved.HasValue && message.TimeStamp > _lastObserved+1)
                await waitSendTask.Task;
        }

        public void Observe(Message message)
        {
            lock(_waitingQueue)
            {
                if(_waitingQueue.ContainsKey(message.TimeStamp + 1))
                    _waitingQueue[message.TimeStamp + 1].SetResult(true);
                _waitingQueue.Remove(message.TimeStamp);
            }
        }
    }
}