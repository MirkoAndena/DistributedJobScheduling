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
using DistributedJobScheduling.Extensions;

namespace DistributedJobScheduling.Client
{
    public class DividersWork : IWork
    {
        private int _start;
        private int _end;

        public DividersWork(int startNumber, int endNumber)
        {
            _start = startNumber;
            _end = endNumber;
        }

        public List<Job> CreateJobs()
        {
            List<Job> jobs = new List<Job>();
            for (int i = _start; i < _end; i++)
                jobs.Add(new DividersJob(i));
            return jobs;
        }

        public void ComputeResult(List<IJobResult> results, string directory)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (IJobResult result in results)
            {
                if (result is DividersResult dividersResult)
                {
                    string res = dividersResult.Dividers == null ? "is prime!" : dividersResult.Dividers.ToString<int>();
                    stringBuilder.Append($"{dividersResult.Number}: {res}{Environment.NewLine}");
                }
            }

            File.WriteAllText(directory + "/result.txt", stringBuilder.ToString());
            Console.WriteLine("Results printed in result.txt");
        }
    }
}