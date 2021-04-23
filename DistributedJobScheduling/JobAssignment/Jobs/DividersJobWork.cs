using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DistributedJobScheduling.JobAssignment.Jobs
{
    public class DividersResult : IJobResult 
    { 
        public int Number;
        public int[] Dividers;

        public DividersResult(int number, int[] dividers)
        {
            this.Number = number;
            this.Dividers = dividers;
        }
    }

    [Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class DividersJobWork : IJobWork
    {
        private int _number;

        [JsonConstructor]
        public DividersJobWork(int number) : base ()
        {
            _number = number;
        }

        public async Task<IJobResult> Run()
        {
            return await Task.Run(() => 
            {
                List<int> dividers = new List<int>();
                for (int i = 2; i < _number; i++)
                    if (_number % i == 0)
                        dividers.Add(i);
                return new DividersResult(_number, dividers.Count > 0 ? dividers.ToArray() : null);
            });
        }
    }
}