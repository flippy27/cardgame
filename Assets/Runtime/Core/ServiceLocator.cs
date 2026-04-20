using System;
using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
            GameLogger.Debug("DI", $"Registered {typeof(T).Name}");
        }

        public static T Resolve<T>() where T : class
        {
            if (!_services.TryGetValue(typeof(T), out var service))
                throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
            return (T)service;
        }

        public static T Get<T>() where T : class => Resolve<T>();

        public static bool TryResolve<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var svc))
            {
                service = (T)svc;
                return true;
            }
            service = null;
            return false;
        }

        public static void Clear()
        {
            _services.Clear();
            GameLogger.Info("DI", "Cleared all services");
        }
    }
}
