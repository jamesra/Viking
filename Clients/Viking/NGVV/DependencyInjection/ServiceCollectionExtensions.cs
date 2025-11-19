using System;
using Microsoft.Extensions.DependencyInjection;
using Viking.Services.Grpc;

namespace Viking.DependencyInjection
{
    /// <summary>
    /// Extension methods for wiring VikingCore services into a dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register shared VikingCore services and required configuration with the provided service collection.
        /// Host applications supply an <see cref="IGrpcServiceConfiguration"/> implementation that bridges
        /// environment-specific settings into the core components.
        /// </summary>
        /// <param name="services">The service collection to populate.</param>
        /// <param name="configuration">Module-provided configuration implementation.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.</exception>
        public static IServiceCollection AddVikingCoreServices(this IServiceCollection services, IGrpcServiceConfiguration configuration)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            services.AddSingleton(configuration);

            return services;
        }
    }
}

