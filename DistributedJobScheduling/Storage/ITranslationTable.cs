namespace DistributedJobScheduling.Storage
{
    public interface ITranslationTable
    {
        int CreateNewIndex { get; }
        int? Get(int localID);
        void Init();
        void StoreIndex(int requestId);
        void Update(int requestId, int job);
        void Remove(int localID);
    }
}