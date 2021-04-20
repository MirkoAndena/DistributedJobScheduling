using System.Linq;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.LifeCycle
{
    public abstract class SystemLifeCycle : ILifeCycle
    {
        private SemaphoreSlim _terminationSemaphore;
        public static Action SystemShutdown;
        protected List<ILifeCycle> _subSystems;

        protected SystemLifeCycle() 
        {
            _subSystems = new List<ILifeCycle>();
            _terminationSemaphore = new SemaphoreSlim(0, 1);
        }

        protected abstract ILogger GetLogger();

        protected abstract void CreateConfiguration(IConfigurationService configurationService, string[] args);

        public void CreateConfiguration(string[] args)
        {
            var configurationService = DependencyManager.Get<IConfigurationService>();
            if (configurationService == null) throw new Exception("IConfigurationService must be created in the constructor");
            CreateConfiguration(configurationService, args);
            
            lock(Console.Out)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(configurationService.ToString());
                Console.WriteLine($"Local IP {NetworkUtils.GetLocalIP()}");
                Console.ResetColor();
            }
        }

        public async Task RunAndWait()
        {
            this.Run();
            await _terminationSemaphore.WaitAsync();
        }

        public void Run()
        {
            SystemShutdown += Shutdown;

            Init();
            InitSubSystems();
            Start();
        } 

        public void Shutdown()
        {
            if (_terminationSemaphore.CurrentCount == 0)
            {
                GetLogger().Flush();
                Stop(); 
                _terminationSemaphore.Release();
                Destroy();
            }
        }
        
        private void Init()
        {
            Console.Write("Building subsystems...");
            CreateSubsystems();
            Console.WriteLine("Done");
        }

        protected abstract void CreateSubsystems(); 
        
        protected void RegisterSubSystem<IT, T>(T instance) where T : IT
        {
            DependencyManager.Instance.RegisterSingletonServiceInstance<IT, T>(instance);
            if (instance is ILifeCycle lifeCycle)
                 _subSystems.Add(lifeCycle);
        }

        protected void RegisterSubSystem<T>(T instance) where T : ILifeCycle
        {
            _subSystems.Add(instance);
        }

        private void InitSubSystems()
        {
            int count = 0;
            _subSystems.ForEach(subsystem => 
            {
                if (subsystem is IInitializable initializable)
                {
                    Console.WriteLine($"Initializing {subsystem.GetType().Name}");
                    initializable.Init();
                    count++;
                }
            });
            Console.WriteLine($"{count} subsystems initialized");
        } 

        private void Start()
        {
            int count = 0;
            _subSystems.ForEach(subsystem => 
            {
                if (subsystem is IStartable startable)
                {
                    Console.WriteLine($"Starting {subsystem.GetType().Name}");
                    startable.Start();
                    count++;
                }
            });
            Console.WriteLine($"{count} subsystems started");
            OnSystemStarted();
        } 

        protected virtual void OnSystemStarted() { }

        private void Stop()
        {
            int count = 0;
            _subSystems.ForEach(subsystem => 
            {
                if (subsystem is IStartable startable)
                {
                    Console.WriteLine($"Stopping {subsystem.GetType().Name}");
                    startable.Stop();
                    count++;
                }
            });
            Console.WriteLine($"{count} subsystems stopped");
        } 

        protected virtual void Destroy()
        {
            Console.WriteLine("System in shutdown");
            Environment.Exit(0);
        }
    }
}