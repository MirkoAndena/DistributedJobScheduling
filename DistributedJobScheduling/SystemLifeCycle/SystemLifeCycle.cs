using System;
using System.Collections.Generic;
using DistributedJobScheduling.Communication;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.Logging;

namespace DistributedJobScheduling.LifeCycle
{
    public abstract class SystemLifeCycle : ILifeCycle
    {
        public static Action Shutdown;
        private List<ILifeCycle> _subSystems;

        protected SystemLifeCycle() 
        { 
            _subSystems = new List<ILifeCycle>();
        }

        protected static void Run(SystemLifeCycle instance)
        {
            Shutdown += delegate 
            { 
                instance.Stop(); 
                instance.Destroy(); 
            };

            instance.Init();
            instance.InitSubSystems();
            instance.Start();
        } 

        public void Init()
        {
            // Create objects instances
            DependencyManager.Instance.RegisterSingletonServiceInstance<ILogger, CsvLogger>(new CsvLogger("../"));
            
            // Create subsystems
            RegisterSubSystem<ICommunicationManager, NetworkManager>(new NetworkManager());
        }
        
        protected void RegisterSubSystem<IT, T>(T instance) where T : IT, ILifeCycle
        {
            DependencyManager.Instance.RegisterSingletonServiceInstance<IT, T>(instance);
            if (instance is ILifeCycle lifeCycle)
                _subSystems.Add(lifeCycle);
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