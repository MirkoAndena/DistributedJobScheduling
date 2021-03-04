using System.Threading;
using System.Threading.Tasks;

namespace DistributedJobScheduling.Communication.Basic
{
    public class Shouter
    {
        private CancellationTokenSource _closeTokenSource;

        public Shouter()
        {
            _closeTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {

        }

        public async Task SendMulticast(Message message)
        {

        }

        public void Close()
        {
            _closeTokenSource?.Cancel();
        }
    }
}