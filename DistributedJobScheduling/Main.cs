using System.Diagnostics;
using System.Threading;
using System;
using DistributedJobScheduling.Configuration;
using System.Threading.Tasks;
using DistributedJobScheduling.LifeCycle;
using DistributedJobScheduling.Client;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.Utils;

namespace DistributedJobScheduling
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            //PoC Client Operation: https://gist.github.com/artumino/6f48700ed3b20571eefc89848de322dd
            SystemLifeCycle system = ArgsUtils.IsPresent(args, "CLIENT") ? (SystemLifeCycle)new ClientSystemManager() : (SystemLifeCycle)new SystemManager();

            try
            {
                system.CreateConfiguration(args);
            }
            catch (Exception e)
            {
                lock(Console.Out)
                {
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                    return;
                }
            }

            if(!Debugger.IsAttached)
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionBehavior);
            
            TaskScheduler.UnobservedTaskException += UnobserveddExceptionBehavior;
            
            await system.RunAndWait();
        }

        static void UnobserveddExceptionBehavior(object sender, UnobservedTaskExceptionEventArgs args)
        {
            Exception e = (Exception) args.Exception;
            var logger = DependencyInjection.DependencyManager.Get<ILogger>();
            logger.Warning(Tag.UnobservedTask, e.Message, e);
            args.SetObserved();
        }

        static void UnhandledExceptionBehavior(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception) args.ExceptionObject;
            var logger = DependencyInjection.DependencyManager.Get<ILogger>();
            logger.Fatal(Tag.UnHandled, e.Message, e);
        }
    }
}