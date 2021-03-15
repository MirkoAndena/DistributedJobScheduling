namespace DistributedJobScheduling.LifeCycle
{
    public interface ILifeCycle
    {
        void Init();
        void Start();
        void Stop();
    }
}