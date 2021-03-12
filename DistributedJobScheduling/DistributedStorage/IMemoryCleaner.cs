namespace DistributedJobScheduling.DistributedStorage
{
    public interface IMemoryCleaner
    {
        /// <summary>
        /// Delete items marked as removed
        /// </summary>
        void CleanLogicRemoved();
    }
}