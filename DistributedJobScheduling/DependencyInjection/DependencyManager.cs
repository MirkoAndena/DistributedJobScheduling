using System.Reflection;
using System;
using System.Collections.Generic;
namespace DistributedJobScheduling.DependencyInjection
{
    /// <summary>
    /// Provides a basic DependencyInjection service to facilitate development and unit testing
    /// </summary>
    public class DependencyManager
    {
        private static DependencyManager _instance;
        public static DependencyManager Instance => _instance ?? (_instance = new DependencyManager());

        private Dictionary<Type, object> _singleInstanceServices;
        private Dictionary<Type, Type> _statefullServiceTypes;

        public enum ServiceType
        {
            Singleton,
            Statefull
        }

        public DependencyManager()
        {
            _singleInstanceServices = new Dictionary<Type, object>();
            _statefullServiceTypes = new Dictionary<Type, Type>();
        }

        public void RegisterService<IT,T>(ServiceType serviceType = ServiceType.Singleton)
            where T : IT
        {
            if(serviceType == ServiceType.Singleton)
                RegisterSingletonServiceInstance<IT, T>(Activator.CreateInstance<T>());
            else
                _statefullServiceTypes.Add(typeof(T), typeof(IT));
        }

        public void RegisterSingletonServiceInstance<IT, T>(T dependencyService)
            where T : IT
        {
            if(!_singleInstanceServices.ContainsKey(typeof(T)))
                _singleInstanceServices.Add(typeof(T), dependencyService);
        }

        public static T Get<T>() => Instance.GetService<T>();
        public T GetService<T>()
        {
            var serviceType = typeof(T);
            if(_singleInstanceServices.ContainsKey(serviceType))
                return (T)_singleInstanceServices[serviceType];
            if(_statefullServiceTypes.ContainsKey(serviceType))
                return (T)Activator.CreateInstance(_statefullServiceTypes[serviceType]);
            return default(T);
        }
    }
}