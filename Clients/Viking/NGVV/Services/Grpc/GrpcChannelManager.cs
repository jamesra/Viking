using System;
using System.Diagnostics;
using System.Threading.Tasks;
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

        /// <summary>The port the segmentation server published for its cleartext listener before it was removed.</summary>
        internal const int LegacyCleartextPort = 40080;

        /// <summary>The port the segmentation server publishes for gRPC over TLS.</summary>
        internal const int TlsPort = 40443;

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

            Channel? stale = null;
            try
            {
                lock (_lock)
                {
                    if (_channel is null ||
                        _currentServiceUrl != channelKey ||
                        _channel.State == ChannelState.Shutdown ||
                        _channel.State == ChannelState.TransientFailure)
                    {
                        stale = DetachChannelUnlocked();

                        ChannelCredentials credentials = useTls ? new SslCredentials() : ChannelCredentials.Insecure;
                        _channel = new Channel(serviceUrl, credentials, SegmentationChannelOptions);
                        _currentServiceUrl = channelKey;

                        Trace.WriteLine($"Created new shared gRPC channel to {serviceUrl} (tls={useTls})");
                    }

                    return _channel;
                }
            }
            finally
            {
                ShutdownDetached(stale, wait: false);
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

            // The segmentation server no longer has a cleartext listener, so every endpoint that
            // parses is dialed with TLS, whatever scheme it was saved with.
            return true;
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
            Channel? stale;
            lock (_lock)
            {
                stale = DetachChannelUnlocked();
                _currentServiceUrl = null;
            }

            // Detached under the lock, shut down after it: a caller on the UI thread is not held for the
            // shutdown and other threads can create the new channel at once.
            ShutdownDetached(stale, wait: false);
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            Channel? stale;
            lock (_lock)
            {
                stale = DetachChannelUnlocked();
                _currentServiceUrl = null;
            }

            ShutdownDetached(stale, wait: true);
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
            if (string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                parsedUri.Port == LegacyCleartextPort)
            {
                // A saved endpoint for the old cleartext port moves to the TLS port.
                string host = parsedUri.HostNameType == UriHostNameType.IPv6 ? $"[{parsedUri.Host}]" : parsedUri.Host;
                authority = $"{host}:{TlsPort}";
            }

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

        /// <summary>Takes the current channel out of service. Caller holds <c>_lock</c> and shuts the result down after releasing it.</summary>
        private Channel? DetachChannelUnlocked()
        {
            Channel? channel = _channel;
            _channel = null;
            return channel;
        }

        /// <summary>
        /// Shuts down a channel that is no longer reachable through the manager. Never call while holding
        /// <c>_lock</c>. With <paramref name="wait"/> false the shutdown runs in the background and a failure is logged.
        /// </summary>
        private static void ShutdownDetached(Channel? channel, bool wait)
        {
            if (channel is null || channel.State == ChannelState.Shutdown)
            {
                return;
            }

            try
            {
                Task shutdown = channel.ShutdownAsync();
                if (wait)
                {
                    shutdown.Wait(TimeSpan.FromSeconds(5));
                    Trace.WriteLine("Shared gRPC channel shut down successfully");
                    return;
                }

                _ = shutdown.ContinueWith(
                    task => Trace.WriteLine(task.IsFaulted
                        ? $"Error shutting down shared gRPC channel: {task.Exception?.GetBaseException().Message}"
                        : "Shared gRPC channel shut down successfully"),
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error shutting down shared gRPC channel: {ex.Message}");
            }
        }
    }
}

