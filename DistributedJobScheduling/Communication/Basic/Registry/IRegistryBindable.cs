namespace DistributedJobScheduling.Communication.Basic
{
    public interface IRegistryBindable
    {
        void BindToRegistry(Node.INodeRegistry registry);
    }
}