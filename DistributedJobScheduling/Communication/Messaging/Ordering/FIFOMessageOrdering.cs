using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.Communication.Messaging.Ordering
{
    public class FIFOMessageOrdering : IMessageOrdering
    {
        private ILogger _logger;
        private int? _lastObserved = null;
        private Dictionary<int, TaskCompletionSource<bool>> _waitingQueue;

        public FIFOMessageOrdering() : this(DependencyInjection.DependencyManager.Get<ILogger>()) { }
        public FIFOMessageOrdering(ILogger logger)
        {
            _logger = logger;
            _waitingQueue = new Dictionary<int, TaskCompletionSource<bool>>();
        }

        public async Task EnsureOrdering(Message message)
        {
            TaskCompletionSource<bool> waitSendTask;
            int? lastObserved = null;
            lock(_waitingQueue)
            {
                if(!_waitingQueue.ContainsKey(message.TimeStamp))
                    _waitingQueue.Add(message.TimeStamp, new TaskCompletionSource<bool>());
                waitSendTask = _waitingQueue[message.TimeStamp];
                lastObserved = _lastObserved;
            }

            if(lastObserved.HasValue && message.TimeStamp > lastObserved+1)
            {
                if(message.TimeStamp > lastObserved + 5)
                    _logger.Error(Tag.CommunicationBasic, $"Message ordering seems to be stuck waiting from message with timestamp { lastObserved+1 }", null);
                await waitSendTask.Task;
            }
        }

        public void Observe(Message message)
        {
            lock(_waitingQueue)
            {
                _lastObserved = message.TimeStamp;
                if(_waitingQueue.ContainsKey(message.TimeStamp + 1))
                    _waitingQueue[message.TimeStamp + 1].SetResult(true);
                _waitingQueue.Remove(message.TimeStamp);
            }
        }

        public async Task OrderedExecute(Message message, Func<Task> _actionToExecute)
        {
            await EnsureOrdering(message);

            try 
            {
                await _actionToExecute?.Invoke();
            }
            catch 
            {
                throw;
            }
            finally
            {
                Observe(message);
            }
        }
    }
}