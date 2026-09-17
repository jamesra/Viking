using System;

namespace Viking.DependencyInjection
{
    /// <summary>
    /// Provides configuration values required by core services that are shared across modules.
    /// Implementations are supplied by the host (for example, WebAnnotation) so that
    /// VikingCore remains free of module-specific static dependencies.
    /// </summary>
    public interface IGrpcServiceConfiguration
    {
        /// <summary>
        /// Resolve the service endpoint that gRPC clients should use.
        /// Implementations may read from configuration files or other runtime sources.
        /// </summary>
        /// <returns>Endpoint in host:port format, or <c>null</c> if segmentation is disabled.</returns>
        string Endpoint();
    }

    public class GrpcServiceConfiguration(string endpoint) : IGrpcServiceConfiguration
    {
        private readonly string _endpoint = endpoint;

        /// <inheritdoc />
        public string Endpoint() => _endpoint;
    }
}

