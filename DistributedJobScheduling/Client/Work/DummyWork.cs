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

        public DummyWork(int timeout)
        {
            this.timeout = timeout;
        }

        public List<Job> CreateJobs()
        {
            List<Job> jobs = new List<Job>();
            for (int i = 0; i < 10; i++)
                jobs.Add(new TimeoutJob(timeout));
            return jobs;
        }

        public void ComputeResult(List<IJobResult> results)
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