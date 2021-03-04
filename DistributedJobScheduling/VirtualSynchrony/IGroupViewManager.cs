using System;
using DistributedJobScheduling.Communication;

namespace DistributedJobScheduling.VirtualSynchrony
{
    /// <summary>
    /// This manager handles all the operations to achieve virtual synchrony inside the group
    /// It also offers methods to other services to contact either all or none of the current executors
    /// It offers the same functionality of a ICommunicationManager
    /// </summary>
    public interface IGroupViewManager : ICommunicationManager
    {
    }
}