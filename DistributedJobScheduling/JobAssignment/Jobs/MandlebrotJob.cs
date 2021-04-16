using System.Drawing;
using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;

namespace DistributedJobScheduling.JobAssignment.Jobs
{
    [Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class MandlebrotResult : IJobResult 
    { 
        public double[,] Value { get; private set; }
        public double Max { get; private set; }
        public Rectangle Rectangle { get; }

        public MandlebrotResult(Rectangle rectangle)
        {
            this.Rectangle = rectangle;
            this.Value = new double[rectangle.Width, rectangle.Height];
            this.Max = 0;
        }

        public void TryUpdateMax(double newMax)
        {
            lock(this)
            {
                if(Max < newMax)
                    Max = newMax;
            }
        }

        public override string ToString()
        {
            return $"{base.ToString()}:{Value.Length}";
        }
    }

    [Serializable]
    [JsonObject(MemberSerialization.Fields)]
    public class MandlebrotJob : Job
    {
        private const double MAX_EXTENT = 2.0;
        private Rectangle _rect;
        private int _totalWidth;
        private int _totalHeight;
        private int _maxIterations;

        [JsonConstructor]
        public MandlebrotJob(Rectangle rect, int totalWidth, int totalHeight, int maxIterations) : base ()
        {
            _rect = rect;
            _totalHeight = totalHeight;
            _totalWidth = totalWidth;
            _maxIterations = maxIterations;
        }

        public override async Task<IJobResult> Run()
        {
            var result = new MandlebrotResult(_rect);
            var xRange = Enumerable.Range(_rect.X, _rect.Width);
            var yRange = Enumerable.Range(_rect.Y, _rect.Height);
            
            var allCoordinates = from x in xRange
                                 from y in yRange
                                 select new {x,y};

            await Task.Run(() => Parallel.ForEach(allCoordinates, (coords) =>
            {
                const double maxNorm = MAX_EXTENT * MAX_EXTENT;
                double scale = 2 * MAX_EXTENT / Math.Min(_totalWidth, _totalHeight);
                double x = (coords.x - (double)_totalWidth / 2) * scale;
                double y = ((double)_totalHeight / 2 - coords.y) * scale;
                
                Complex z = new Complex(0, 0);
                Complex alpha = new Complex(x , y);

                int iter = 0;
                while(Complex.Abs(z)<maxNorm && iter < _maxIterations ) {
                    z = Complex.Pow(z,2) + alpha;
                    iter++;
                }

                double value =  iter < _maxIterations ? 0 : (double)iter / (double)_maxIterations;
                result.Value[coords.x - _rect.X, coords.y - _rect.Y] = value;
                result.TryUpdateMax(value);
            }));
            
            return result;
        }
    }
}