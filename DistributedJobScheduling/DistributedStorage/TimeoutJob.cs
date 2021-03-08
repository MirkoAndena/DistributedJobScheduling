using System;
using System.Threading.Tasks;

namespace DistributedJobScheduling.DistributedStorage
{
    public class BooleanJobResult : IJobResult 
    { 
        public bool Value;

        public BooleanJobResult(bool value)
        {
            this.Value = value;
        }
    }

    public class TimeoutJob : Job
    {
        private int _seconds;

        public TimeoutJob(int seconds) : base ()
        {
            _seconds = seconds;
        }

        public override async Task<IJobResult> Run()
        {
            await Task.Delay(TimeSpan.FromSeconds(_seconds));
            return new BooleanJobResult(true);
        }
    }
}