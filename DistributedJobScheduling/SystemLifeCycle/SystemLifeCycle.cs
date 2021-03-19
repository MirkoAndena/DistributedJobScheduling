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
            _instance.Start();
        } 

        private void InitSubSystem<IT, T>(T instance) where T : IT, ILifeCycle
        {
            DependencyManager.Instance.RegisterSingletonServiceInstance<IT, T>(instance);
            _subSystems.Add(instance);
        }

        // todo dependency inj senza interfaccia
        private void InitSubSystem<T>(T instance) where T : ILifeCycle
        {
            DependencyManager.Instance.RegisterService<T, T>(DependencyManager.ServiceType.Statefull);
            _subSystems.Add(instance);
        }

        public void Init()
        {
            DependencyManager.Instance.RegisterSingletonServiceInstance<ILogger, CsvLogger>(new CsvLogger("../"));
            
            InitSubSystem<ICommunicationManager, NetworkManager>(new NetworkManager());
        }

        public void Start() => _subSystems.ForEach(subsystem => subsystem.Start());

        public void Stop() => _subSystems.ForEach(subsystem => subsystem.Stop());

        private void Destroy()
        {
            Environment.Exit(0);
        }
    }
}