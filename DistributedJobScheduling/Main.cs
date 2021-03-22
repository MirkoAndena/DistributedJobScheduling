using System;
using DistributedJobScheduling.Configuration;

namespace DistributedJobScheduling
{
    public class Program
    {
        static void Main(string[] args)
        {
            SystemManager systemManager = new SystemManager();
            var config = DependencyInjection.DependencyManager.Get<IConfigurationService>();

            if (!CreateConfiguration(config, args))
            {
                Console.WriteLine("ID not specified on launch");
                return;
            }
            
            systemManager.Run();
        }

        private static bool CreateConfiguration(IConfigurationService config, string[] args)
        {
            int id;
            bool isId = Int32.TryParse(args.Length > 0 ? args[0] : "", out id);
            bool coordinator = args.Length > 1 && args[1].ToLower() == "coordinator";
            
            if (!isId) return false;
            
            config.SetValue<int>("nodeId", id);
            config.SetValue<bool>("coordinator", coordinator);
            return true;
        }
    }
}