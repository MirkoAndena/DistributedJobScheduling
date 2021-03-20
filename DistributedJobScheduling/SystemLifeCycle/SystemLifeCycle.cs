using System;
using System.Collections.Generic;
using DistributedJobScheduling.DependencyInjection;

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

        public void Run()
        {
            Shutdown += delegate 
            { 
                Stop(); 
                Destroy(); 
            };

            Init();
            InitSubSystems();
            Start();
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