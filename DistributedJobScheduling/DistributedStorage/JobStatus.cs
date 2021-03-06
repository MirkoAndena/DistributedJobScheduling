namespace DistributedJobScheduling.DistributedStorage
{
    public enum JobStatus 
    {
        PENDING,
        RUNNING,
        COMPLETED,
        REMOVED
    }

    public class Job
    {
        private JobStatus _status;
        private int _id;
        private int _node;
    }
}