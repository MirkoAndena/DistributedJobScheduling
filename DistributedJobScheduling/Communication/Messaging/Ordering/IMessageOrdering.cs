using System.Text;
using System.Threading.Tasks;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging.Ordering
{
    public interface IMessageOrdering
    {
        Task EnsureOrdering(Message message);
        void Observe(Message message);
    }
}