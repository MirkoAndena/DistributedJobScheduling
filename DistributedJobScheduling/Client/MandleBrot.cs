using System.Collections.Generic;
using System.Drawing;
using DistributedJobScheduling.JobAssignment.Jobs;

namespace DistributedJobScheduling.Client
{
    public static class Mandlebrot
    {
        public static List<MandlebrotJob> CreateJobs(int batches, int dimension, int iterations)
        {
            List<MandlebrotJob> jobs = new List<MandlebrotJob>();
            int batchesPerSide = batches / 2;
            for(int i = 0; i < batchesPerSide; i++)
            {
                int horizontalBatchSize = dimension / batchesPerSide;
                int startingX = i * horizontalBatchSize;
                for(int j = 0; j < batchesPerSide; j++)
                {
                    int verticalBatchSize = dimension / batchesPerSide;
                    int startingY = j * verticalBatchSize;
                   jobs.Add(new MandlebrotJob(new Rectangle(startingX, startingY, horizontalBatchSize, verticalBatchSize), dimension, dimension, iterations));
                }
            }
            return jobs;
        }
    }
}