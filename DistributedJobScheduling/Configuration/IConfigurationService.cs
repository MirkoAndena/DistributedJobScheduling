namespace DistributedJobScheduling.Configuration
{
    public interface IConfigurationService
    {
        T GetValue<T>(string key, T defaultValue = default(T));

        void SetValue<T>(string key, T value);
    }
}