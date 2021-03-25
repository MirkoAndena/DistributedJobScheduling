namespace DistributedJobScheduling.Serialization
{
    public interface ISerializer
    {
        byte[] Serialize(object o);
        T Deserialize<T>(byte[] serialized);
    }
}