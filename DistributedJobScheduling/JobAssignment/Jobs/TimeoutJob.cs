using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DistributedJobScheduling.JobAssignment.Jobs
{
    public class BooleanJobResult : IJobResult 
    { 
        public bool Value;

        public BooleanJobResult(bool value)
        {
            this.Value = value;
        }
    }

    [Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class TimeoutJob : Job
    {
        private int _seconds;

        [JsonConstructor]
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