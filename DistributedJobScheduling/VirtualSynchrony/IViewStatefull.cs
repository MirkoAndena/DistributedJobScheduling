using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.VirtualSynchrony
{
    /// <summary>
    /// Implemented by services which have some state objects that depend on the current view
    /// </summary>
    public interface IViewStatefull
    {
        Message ToSyncMessage();
        void OnViewSync(Message syncMessage);
    }
}