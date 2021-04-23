using System.Text;
using System.Net.Mime;
using System.Net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DistributedJobScheduling.JobAssignment.Jobs;
using SkiaSharp;
using DistributedJobScheduling.Client.Work;

namespace DistributedJobScheduling.Client
{
    public class DummyWork : IWork
    {
        private int timeout;
        private int count;

        public DummyWork(int timeout, int count)
        {
            this.timeout = timeout;
            this.count = count;
        }

        public List<IJobWork> CreateJobs()
        {
            List<IJobWork> jobs = new List<IJobWork>();
            for (int i = 0; i < count; i++)
                jobs.Add(new TimeoutJobWork(timeout));
            return jobs;
        }

        public void ComputeResult(List<IJobResult> results, string directory)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (IJobResult result in results)
            {
                if (result is BooleanJobResult booleanJobResult)
                    stringBuilder.Append(booleanJobResult.Value + " ");
            }
            Console.WriteLine($"Results: {stringBuilder.ToString()}");
        }
    }
}