using Microsoft.Extensions.DependencyInjection;

namespace Viking.Common
{
    /// <summary>
    /// Extension modules implement this to register their dependencies with the host DI container.
    /// </summary>
    public interface IModuleServiceRegistrar
    {
        /// <summary>
        /// Add services required by the module to the provided collection.
        /// </summary>
        /// <param name="services">Service collection to populate.</param>
        void RegisterServices(IServiceCollection services);
    }
}

