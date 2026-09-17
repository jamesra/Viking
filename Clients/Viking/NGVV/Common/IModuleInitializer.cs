using System;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.Common
{
    /// <summary>
    /// Extension modules implement this to perform runtime initialization after the DI container is built.
    /// </summary>
    public interface IModuleInitializer
    {
        /// <summary>
        /// Execute module-specific initialization using the application service provider.
        /// </summary>
        /// <param name="serviceProvider">Built service provider that can be used to resolve dependencies.</param>
        /// <param name="cancellationToken">Cancellation signal provided by the host.</param>
        Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
    }
}

