using System;
using System.Collections.Generic;

namespace PutPicture.Core
{
    /// <summary>
    /// 简单的依赖注入容器
    /// </summary>
    public class ServiceContainer
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Func<ServiceContainer, object>> _factories = new Dictionary<Type, Func<ServiceContainer, object>>();
        
        /// <summary>
        /// 注册单例服务
        /// </summary>
        public void RegisterSingleton<TInterface, TImplementation>(TImplementation instance)
            where TImplementation : class, TInterface
        {
            _services[typeof(TInterface)] = instance;
        }
        
        /// <summary>
        /// 注册服务工厂
        /// </summary>
        public void RegisterFactory<TInterface>(Func<ServiceContainer, TInterface> factory)
        {
            _factories[typeof(TInterface)] = container => factory(container);
        }
        
        /// <summary>
        /// 获取服务
        /// </summary>
        public T GetService<T>()
        {
            var type = typeof(T);
            
            // 先查找已注册的实例
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            
            // 再查找工厂方法
            if (_factories.TryGetValue(type, out var factory))
            {
                var instance = factory(this);
                _services[type] = instance; // 缓存实例
                return (T)instance;
            }
            
            throw new InvalidOperationException($"Service of type {type.Name} is not registered.");
        }
        
        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public bool IsRegistered<T>()
        {
            var type = typeof(T);
            return _services.ContainsKey(type) || _factories.ContainsKey(type);
        }
    }
}