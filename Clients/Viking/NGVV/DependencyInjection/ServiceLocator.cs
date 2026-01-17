using System;
using Microsoft.Extensions.DependencyInjection;
using Viking.Services.Grpc;

namespace Viking.DependencyInjection
{
    /// <summary>
    /// Provides centralized access to services resolved from the configured dependency injection container.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly object _lock = new object();
        private static IServiceProvider? _serviceProvider;
        private static IServiceCollection? _serviceCollection;
        private static bool _isInitialized;

        /// <summary>
        /// Indicates whether the service locator has been initialized.
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes the service locator with the specified service provider.
        /// Should be called once during application startup.
        /// </summary>
        /// <param name="serviceProvider">Fully built service provider instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
        public static void Initialize(IServiceProvider serviceProvider)
            => Initialize(serviceProvider, null);

        public static void Initialize(IServiceProvider serviceProvider, IServiceCollection? services)
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            lock (_lock)
            {
                if (_isInitialized && _serviceProvider is IDisposable disposableExisting)
                {
                    disposableExisting.Dispose();
                }

                _serviceProvider = serviceProvider;
                _serviceCollection = services;
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Gets the underlying <see cref="IServiceProvider"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the service locator has not been initialized.</exception>
        public static IServiceProvider ServiceProvider
            => _serviceProvider ?? throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");

        /// <summary>
        /// Resolve a service from the container, throwing if it is missing.
        /// </summary>
        /// <typeparam name="T">Type of service to resolve.</typeparam>
        public static T GetRequiredService<T>() => ServiceProvider.GetRequiredService<T>();

        /// <summary>
        /// Resolve a service from the container, returning null if it is missing.
        /// </summary>
        /// <typeparam name="T">Type of service to resolve.</typeparam>
        public static T GetService<T>() => ServiceProvider.GetService<T>();

        /// <summary>
        /// Convenience accessor for the shared <see cref="IGrpcChannelManager"/> instance.
        /// </summary>
        public static IGrpcChannelManager GrpcChannelManager => GetRequiredService<IGrpcChannelManager>();

        /// <summary>
        /// Resets the service locator. Useful for testing or re-initialization scenarios.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                if (_serviceProvider is IServiceProvider provider)
                {
                    if (provider.GetService<IGrpcChannelManager>() is IGrpcChannelManager manager)
                    {
                        manager.Shutdown();
                    }

                    if (provider is IDisposable disposableProvider)
                    {
                        disposableProvider.Dispose();
                    }
                }

                _serviceProvider = null;
                _serviceCollection = null;
                _isInitialized = false;
            }
        }

        public static void RebuildServiceProvider(Action<IServiceCollection> configureServices)
        {
            if (configureServices is null)
            {
                throw new ArgumentNullException(nameof(configureServices));
            }

            lock (_lock)
            {
                if (_serviceCollection is null)
                {
                    throw new InvalidOperationException("Service collection is not available for rebuilding.");
                }

                configureServices(_serviceCollection);

                if (_serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _serviceProvider = _serviceCollection.BuildServiceProvider();
                _isInitialized = true;
            }
        }
    }
}

