using System;
using System.Text;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging.Ordering
{
    public interface IMessageOrdering
    {
        /// <summary>
        /// Execute an operations ensuring FIFO Ordering and correctly handles Exceptions (if message 1 fails message 2 can proceed)
        /// </summary>
        /// <param name="message">Message tied to this execution</param>
        /// <param name="_actionToExecute">Action to execute with the message</param>
        Task OrderedExecute(Message message, Func<Task> _actionToExecute);

        /// <summary>
        /// Ensures FIFO Ordering, blocks until operations on this message can proceed.
        /// WARNING: If observe is not called (for example in Exceptions) the ordering can be permanently broken
        /// </summary>
        /// <param name="message">Message tied to this execution</param>
        Task EnsureOrdering(Message message);

        /// <summary>
        /// Unlocks execution of the message directly following this one
        /// WARNING: If observe is not called (for example in Exceptions) the ordering can be permanently broken
        /// </summary>
        /// <param name="message">Message tied to this execution</param>
        void Observe(Message message);
    }
}