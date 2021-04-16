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

namespace DistributedJobScheduling.Client
{
    public class Mandlebrot
    {
        private int batches;
        private int dimension;

        public Mandlebrot(int batches, int dimension)
        {
            this.batches = batches;
            this.dimension = dimension;
        }

        public List<MandlebrotJob> CreateJobs(int iterations)
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

        private SKColor GetColor(double value)
        {
            byte color = (byte)(255 * value);
            return new SKColor(color, color, color, 255);
        }

        public void CreateImage(List<IJobResult> results)
        {
            var batchDim = dimension / batches;
            SKBitmap image = new SKBitmap(batchDim, batchDim, true);
            
            var xRange = Enumerable.Range(0, batchDim);
            var yRange = Enumerable.Range(0, batchDim);
            var allCoordinates = from x in xRange
                                 from y in yRange
                                 select new {x,y};

            Parallel.ForEach(allCoordinates, (coords) =>
            {
                var result = ChooseCorrectResult(results, coords.x, coords.y);
                image.SetPixel(coords.x, coords.y, GetColor(result.Value[coords.x,coords.y] / result.Max));
            });
            
            Console.WriteLine("Encoding");
            using(FileStream fileStream = new FileStream("output.jpg", FileMode.Create))
                image.Encode(fileStream, SKEncodedImageFormat.Jpeg, 98);
        }

        private MandlebrotResult ChooseCorrectResult(List<IJobResult> results, int x, int y)
        {
            foreach (IJobResult result in results)
            {
                MandlebrotResult mr = (MandlebrotResult)result;
                if (mr.Rectangle.Contains(new Point(x, y)))
                    return mr;
            }

            throw new Exception($"Requested a point ({x}, {y}) not present in any result");
        }
    }
}