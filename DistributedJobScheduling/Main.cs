using System.Threading;
using System;
using DistributedJobScheduling.Configuration;
using System.Threading.Tasks;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Client;

namespace DistributedJobScheduling
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            SystemLifeCycle system = IsClient(args) ? (SystemLifeCycle)new ClientSystemManager() : (SystemLifeCycle)new SystemManager();

            try
            {
                system.CreateConfiguration(args);
            }
            catch (Exception e)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();
            }
            
            await system.Run();
        }

        private static bool IsClient(string[] args)
        {
            if (Environment.GetEnvironmentVariable("CLIENT") == "true")
                return true;
            if (args.Length > 0)
                return args[0].Trim().ToLower() == "client";
            return false;
        }
    }
}