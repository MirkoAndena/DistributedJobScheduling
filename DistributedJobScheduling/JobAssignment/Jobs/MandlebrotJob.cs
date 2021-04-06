using System;
using System.Numerics;
using System.Threading.Tasks;

namespace DistributedJobScheduling.JobAssignment.Jobs
{
    public class DoubleResult : IJobResult 
    { 
        public double Value;

        public DoubleResult(double value)
        {
            this.Value = value;
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    [Serializable]
    public class MandlebrotJob : Job
    {
        private double _x;
        private double _y;
        private int _maxIterations;

        public MandlebrotJob(double x, double y, int maxIterations) : base ()
        {
            _x = x;
            _y = y;
            _maxIterations = maxIterations;
        }

        public override async Task<IJobResult> Run()
        {
            int iter = 0;
            await Task.Run(() => {

                Complex z = new Complex(0, 0);
                Complex alpha = new Complex(_x , _y);
                while( Complex.Abs(z)<4 && iter < _maxIterations ) {
                        z = Complex.Pow(z,2) + alpha;
                        iter++;
                }
            });
            return new DoubleResult((double)iter/(double)_maxIterations);
        }
    }
}