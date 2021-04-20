using System.Threading;
using System;
using DistributedJobScheduling.Configuration;
using System.Threading.Tasks;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Client;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            //PoC Client Operation: https://gist.github.com/artumino/6f48700ed3b20571eefc89848de322dd
            SystemLifeCycle system = IsClient(args) ? (SystemLifeCycle)new ClientSystemManager() : (SystemLifeCycle)new SystemManager();

            try
            {
                system.CreateConfiguration(args);
            }
            catch (Exception e)
            {
                lock(Console.Out)
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                }
            }

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionBehavior);
            
            await system.RunAndWait();
        }

        private static bool IsClient(string[] args)
        {
            if (Environment.GetEnvironmentVariable("CLIENT") == "true")
                return true;
            if (args.Length > 0)
                return args[0].Trim().ToLower() == "client";
            return false;
        }

        static void UnhandledExceptionBehavior(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception) args.ExceptionObject;
            var logger = DependencyInjection.DependencyManager.Get<ILogger>();
            logger.Fatal(Tag.UnHandled, e.Message, e);
        }
    }
}