namespace DistributedJobScheduling.LifeCycle
{
    public interface IInitializable : ILifeCycle
    {
        void Init();
    }
}