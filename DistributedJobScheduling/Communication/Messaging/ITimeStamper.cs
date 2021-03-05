using System.Text;
using DistributedJobScheduling.Communication.Basic;

namespace DistributedJobScheduling.Communication.Messaging
{
    public interface ITimeStamper
    {
        int CreateTimeStamp();
    }
}