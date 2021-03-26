using System.Threading;
using System;
using DistributedJobScheduling.Configuration;
using System.Threading.Tasks;

namespace DistributedJobScheduling
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            SystemManager systemManager = new SystemManager();
            var config = DependencyInjection.DependencyManager.Get<IConfigurationService>();

            if (!CreateConfiguration(config, args))
            {
                Console.WriteLine("ID not specified on launch");
                return;
            }
            
            await systemManager.Run();
        }

        private static bool CreateConfiguration(IConfigurationService config, string[] args)
        {
            int id;
            bool isId = Int32.TryParse(args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("NODE_ID"), out id);
            bool coordinator = (args.Length > 1 && args[1].ToLower() == "coordinator") || (Environment.GetEnvironmentVariable("COORD") != null);
            
            if (!isId) return false;
            
            Console.WriteLine($"Configuration nodeId: {id}");
            config.SetValue<int?>("nodeId", id);
            config.SetValue<bool>("coordinator", coordinator);
            return true;
        }
    }
}