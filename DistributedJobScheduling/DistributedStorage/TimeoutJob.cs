using System;
using System.Threading.Tasks;

namespace DistributedJobScheduling.DistributedStorage
{
    public class TimeoutJob : Job<bool>
    {
        private int _seconds;

        public TimeoutJob(int seconds) : base ()
        {
            _seconds = seconds;
        }

        public override async Task<bool> Run()
        {
            await Task.Delay(TimeSpan.FromSeconds(_seconds));
            return true;
        }
    }
}