using System.Buffers.Text;
using System.Runtime.Intrinsics.Arm.Arm64;
using DistributedJobScheduling.LifeCycle;

namespace DistributedJobScheduling
{
    public class System : SystemLifeCycle
    {
        private static System _instance;

        private System() : base()
        {

        }

        public static void Run()
        {
            _instance = new System();
            _instance.Run();
        }       
    }
}