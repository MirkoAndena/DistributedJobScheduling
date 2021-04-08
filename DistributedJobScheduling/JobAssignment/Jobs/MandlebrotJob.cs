using System.Drawing;
using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Linq;

namespace DistributedJobScheduling.JobAssignment.Jobs
{
    public class MandlebrotResult : IJobResult 
    { 
        public double[,] Value { get; private set; }
        public double Max { get; private set; }

        public MandlebrotResult(int width, int height)
        {
            this.Value = new double[width, height];
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
            return base.ToString();
        }
    }

    [Serializable]
    public class MandlebrotJob : Job
    {
        private Rectangle _rect;
        private int _totalWidth;
        private int _totalHeight;
        private int _maxIterations;

        public MandlebrotJob(Rectangle rect, int totalWidth, int totalHeight, int maxIterations) : base ()
        {
            _rect = rect;
            _totalHeight = totalHeight;
            _totalWidth = totalWidth;
            _maxIterations = maxIterations;
        }

        public override async Task<IJobResult> Run()
        {
            int iter = 0;
            var result = new MandlebrotResult(_rect.Width, _rect.Height);
            var xRange = Enumerable.Range(_rect.X, _rect.X + _rect.Width);
            var yRange = Enumerable.Range(_rect.Y, _rect.Y + _rect.Height);
            
            var allCoordinates = from x in xRange
                                 from y in yRange
                                 select new {x,y};

            var mandlebrotTasks = allCoordinates.Select(async (coords) => await Task.Run(() =>
            {
                double _x = (double)coords.x / (double)_totalWidth;
                double _y = (double)coords.y / (double)_totalHeight;
                Complex z = new Complex(0, 0);
                Complex alpha = new Complex(_x , _y);
                while( Complex.Abs(z)<4 && iter < _maxIterations ) {
                        z = Complex.Pow(z,2) + alpha;
                        iter++;
                }
                double value = (double)iter / (double)_maxIterations;
                result.Value[coords.x, coords.y] = value;
                result.TryUpdateMax(value);
            }));
            await Task.WhenAll(mandlebrotTasks);

            return result;
        }
    }
}