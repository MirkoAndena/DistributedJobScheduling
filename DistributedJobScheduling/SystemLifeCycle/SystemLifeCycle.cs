using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DistributedJobScheduling.Configuration;
using DistributedJobScheduling.DependencyInjection;

namespace DistributedJobScheduling.LifeCycle
{
    public abstract class SystemLifeCycle : ILifeCycle
    {
        private SemaphoreSlim _terminationSemaphore;
        public static Action Shutdown;
        private List<ILifeCycle> _subSystems;

        protected SystemLifeCycle() 
        {
            _subSystems = new List<ILifeCycle>();
            _terminationSemaphore = new SemaphoreSlim(0, 1);
        }

        protected abstract void CreateConfiguration(IConfigurationService configurationService, string[] args);

        public void CreateConfiguration(string[] args)
        {
            var configurationService = DependencyManager.Get<IConfigurationService>();
            if (configurationService == null) throw new Exception("IConfigurationService must be created in the constructor");
            CreateConfiguration(configurationService, args);
        }

        public async Task Run()
        {
            Shutdown += delegate 
            { 
                Stop(); 
                _terminationSemaphore.Release();
                Destroy();
            };

            Init();
            InitSubSystems();
            Start();
            await _terminationSemaphore.WaitAsync();
        } 

        public void Init() => CreateSubsystems();

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