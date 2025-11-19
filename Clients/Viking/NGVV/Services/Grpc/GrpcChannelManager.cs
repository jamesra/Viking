using System;
using System.Diagnostics;
using Grpc.Core;
using Viking.DependencyInjection;

namespace Viking.Services.Grpc
{
    /// <summary>
    /// Manages a shared gRPC channel for the segmentation service to avoid expensive channel creation.
    /// </summary>
    public class GrpcChannelManager : IGrpcChannelManager
    {
        private readonly object _lock = new object();
        private readonly IGrpcServiceConfiguration _configuration;
        private Channel _channel;
        private string _currentServiceUrl;

        public GrpcChannelManager(IGrpcServiceConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <inheritdoc />
        public Channel GetOrCreateChannel()
        {
            string serviceUrl = _configuration.Endpoint();

            serviceUrl = FormatServiceUrl(serviceUrl);

            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                return null;
            }

            lock (_lock)
            {
                if (_channel == null ||
                    _currentServiceUrl != serviceUrl ||
                    _channel.State == ChannelState.Shutdown ||
                    _channel.State == ChannelState.TransientFailure)
                {
                    ShutdownChannelInternal();

                    _channel = new Channel(serviceUrl, ChannelCredentials.Insecure);
                    _currentServiceUrl = serviceUrl;

                    Trace.WriteLine($"Created new shared gRPC channel to {serviceUrl}");
                }

                return _channel;
            }
        }

        /// <inheritdoc />
        public bool IsChannelHealthy()
        {
            lock (_lock)
            {
                return _channel != null &&
                       _channel.State != ChannelState.Shutdown &&
                       _channel.State != ChannelState.TransientFailure;
            }
        }

        /// <inheritdoc />
        public void ResetChannel()
        {
            lock (_lock)
            {
                ShutdownChannelInternal();
                _currentServiceUrl = null;
            }
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            lock (_lock)
            {
                ShutdownChannelInternal();
                _currentServiceUrl = null;
            }
        }

        /// <summary>
        /// Grpc service URLs must be in the form host:port[/path][?query].  This function attempts to format a raw endpoint string to be compatible with that expectation.
        /// </summary>
        /// <param name="rawEndpoint"></param>
        /// <returns></returns>
        private static string FormatServiceUrl(string rawEndpoint)
        {
            if (string.IsNullOrWhiteSpace(rawEndpoint))
            {
                return null;
            }

            string trimmedEndpoint = rawEndpoint.Trim();

            // Ensure we can parse the endpoint by supplying a default scheme if one is missing.
            bool containsScheme = trimmedEndpoint.IndexOf("://", StringComparison.Ordinal) >= 0;
            string endpointToParse = containsScheme ? trimmedEndpoint : $"http://{trimmedEndpoint}";

            if (!Uri.TryCreate(endpointToParse, UriKind.Absolute, out Uri parsedUri))
            {
                return null;
            }

            string authority = parsedUri.Authority;
            string absolutePath = parsedUri.AbsolutePath;

            if (string.Equals(absolutePath, "/", StringComparison.Ordinal))
            {
                absolutePath = string.Empty;
            }

            string query = parsedUri.Query;

            return $"{authority}{absolutePath}{query}";
        }

        private void ShutdownChannelInternal()
        {
            if (_channel == null || _channel.State == ChannelState.Shutdown)
            {
                return;
            }

            try
            {
                _channel.ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
                Trace.WriteLine("Shared gRPC channel shut down successfully");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error shutting down shared gRPC channel: {ex.Message}");
            }
            finally
            {
                _channel = null;
            }
        }
    }
}

