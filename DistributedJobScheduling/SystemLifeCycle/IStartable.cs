namespace DistributedJobScheduling.LifeCycle
{
    public interface IStartable : ILifeCycle
    {
        void Start();
        void Stop();
    }
}