using System.Drawing;
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
            if(!message.TimeStamp.HasValue)
                return;

            TaskCompletionSource<bool> waitSendTask = null;
            int? lastObserved = null;
            bool shouldWait = false;
            lock(_waitingQueue)
            {
                lastObserved = _lastObserved;
                shouldWait = _lastObserved.HasValue && message.TimeStamp.Value > _lastObserved+1;
                if(shouldWait && !_waitingQueue.ContainsKey(message.TimeStamp.Value))
                {
                    _waitingQueue.Add(message.TimeStamp.Value, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                    waitSendTask = _waitingQueue[message.TimeStamp.Value];
                }
            }

            if(shouldWait)
            {
                _logger.Error(Tag.CommunicationBasic, $"Message ordering seems to be stuck waiting from message with timestamp { lastObserved+1 }", null);
                await waitSendTask.Task;
            }
        }

        public void Observe(Message message)
        {
            if(!message.TimeStamp.HasValue)
                return;
                
            lock(_waitingQueue)
            {
                if(_lastObserved < message.TimeStamp.Value)
                {
                    _lastObserved = message.TimeStamp.Value;
                    if(_waitingQueue.ContainsKey(message.TimeStamp.Value + 1))
                        _waitingQueue[message.TimeStamp.Value + 1].SetResult(true);
                    _waitingQueue.Remove(message.TimeStamp.Value);
                }
            }
        }

        public async Task OrderedExecute(Message message, Func<Task> _actionToExecute)
        {
            Console.WriteLine($"Starting {message.TimeStamp}  {message}");
            await EnsureOrdering(message);

            try 
            {
                Console.WriteLine($"Can execute {message.TimeStamp} {message}");
                await _actionToExecute?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                throw;
            }
            finally
            {
                Console.WriteLine($"Advancing timestamp to {message.TimeStamp + 1}");
                Observe(message);
            }
        }
    }
}