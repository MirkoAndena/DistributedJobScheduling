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
    public class TimeoutJobWork : IJobWork
    {
        private int _seconds;

        [JsonConstructor]
        public TimeoutJobWork(int seconds) : base ()
        {
            _seconds = seconds;
        }

        public async Task<IJobResult> Run()
        {
            await Task.Delay(TimeSpan.FromSeconds(_seconds));
            return new BooleanJobResult(true);
        }
    }
}