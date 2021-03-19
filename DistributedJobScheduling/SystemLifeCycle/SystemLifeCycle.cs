using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.LifeCycle
{
    public class SystemLifeCycle : ILifeCycle
    {
        private static SystemLifeCycle _instance;
        public static Action Shutdown;
        private List<ILifeCycle> _subSystems;

        private SystemLifeCycle() 
        { 
            _subSystems = new List<ILifeCycle>();
        }

        public static void Run()
        {
            _instance = new SystemLifeCycle();

            Shutdown += delegate 
            { 
                _instance.Stop(); 
                _instance.Destroy(); 
            };

            _instance.Init();
            _instance.InitSubSystems();
            _instance.Start();
        }       

        public void Init()
        {
            // Create objects instances
            DependencyManager.Instance.RegisterSingletonServiceInstance<ILogger, CsvLogger>(new CsvLogger("../"));
            
            // Create subsystems
            RegisterSubSystem<ICommunicationManager, NetworkManager>(new NetworkManager());
        }
        
        private void RegisterSubSystem<IT, T>(T instance) where T : IT, ILifeCycle
        {
            DependencyManager.Instance.RegisterSingletonServiceInstance<IT, T>(instance);
            _subSystems.Add(instance);
        }

        public void InitSubSystems()
        {
            _subSystems.ForEach(subsystem => 
            {
                if (subsystem is IInitializable initializable)
                    initializable.Init();
            });
        } 

        public void Start()
        {
            _subSystems.ForEach(subsystem => 
            {
                if (subsystem is IStartable startable)
                    startable.Start();
            });
        } 

        public void Stop()
        {
            _subSystems.ForEach(subsystem => 
            {
                if (subsystem is IStartable startable)
                    startable.Stop();
            });
        } 

        private void Destroy()
        {
            Environment.Exit(0);
        }
    }
}