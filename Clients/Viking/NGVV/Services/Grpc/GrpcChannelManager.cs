using System;
using System.Diagnostics;
using Grpc.Core;
using Viking.DependencyInjection;

namespace Viking.Services.Grpc
{
    /// <summary>
    /// Manages a shared gRPC channel for the segmentation service to avoid expensive channel creation.
    /// </summary>
    public class GrpcChannelManager(IGrpcServiceConfiguration configuration) : IGrpcChannelManager
    {
        /// <summary>Max message size (64 MB) to match the segmentation server configuration.</summary>
        private const int MaxMessageSizeBytes = 64 * 1024 * 1024;

        private static readonly ChannelOption[] SegmentationChannelOptions =
        {
            new(ChannelOptions.MaxReceiveMessageLength, MaxMessageSizeBytes),
            new(ChannelOptions.MaxSendMessageLength, MaxMessageSizeBytes)
        };

        private readonly object _lock = new();
        private readonly IGrpcServiceConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        private Channel? _channel;
        private string? _currentServiceUrl;

        /// <inheritdoc />
        public Channel? GetOrCreateChannel()
        {
            string rawEndpoint = _configuration.Endpoint();
            bool useTls = EndpointRequiresTls(rawEndpoint);
            string? serviceUrl = FormatServiceUrl(rawEndpoint);

            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                return null;
            }

            string channelKey = (useTls ? "https://" : "http://") + serviceUrl;

            lock (_lock)
            {
                if (_channel is null ||
                    _currentServiceUrl != channelKey ||
                    _channel.State == ChannelState.Shutdown ||
                    _channel.State == ChannelState.TransientFailure)
                {
                    ShutdownChannelInternal();

                    ChannelCredentials credentials = useTls ? new SslCredentials() : ChannelCredentials.Insecure;
                    _channel = new Channel(serviceUrl, credentials, SegmentationChannelOptions);
                    _currentServiceUrl = channelKey;

                    Trace.WriteLine($"Created new shared gRPC channel to {serviceUrl} (tls={useTls})");
                }

                return _channel;
            }
        }

        /// <summary>
        /// Public HTTPS endpoints and port 443 use the system trust store. Other targets stay cleartext.
        /// </summary>
        internal static bool EndpointRequiresTls(string rawEndpoint)
        {
            if (!TryParseEndpoint(rawEndpoint, out Uri? parsedUri) || parsedUri is null)
            {
                return false;
            }

            if (string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return parsedUri.Port == 443;
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
        private static string? FormatServiceUrl(string rawEndpoint)
        {
            if (string.IsNullOrWhiteSpace(rawEndpoint))
            {
                return null;
            }

            if (!TryParseEndpoint(rawEndpoint, out Uri? parsedUri) || parsedUri is null)
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

        /// <summary>
        /// Parse host:port or an absolute URI. A missing scheme is treated as http so the port can be read.
        /// </summary>
        private static bool TryParseEndpoint(string rawEndpoint, out Uri? parsedUri)
        {
            parsedUri = null;
            if (string.IsNullOrWhiteSpace(rawEndpoint))
            {
                return false;
            }

            string trimmedEndpoint = rawEndpoint.Trim();
            bool containsScheme = trimmedEndpoint.IndexOf("://", StringComparison.Ordinal) >= 0;
            string endpointToParse = containsScheme ? trimmedEndpoint : $"http://{trimmedEndpoint}";
            return Uri.TryCreate(endpointToParse, UriKind.Absolute, out parsedUri) && parsedUri is not null;
        }

        private void ShutdownChannelInternal()
        {
            if (_channel is null || _channel.State == ChannelState.Shutdown)
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

