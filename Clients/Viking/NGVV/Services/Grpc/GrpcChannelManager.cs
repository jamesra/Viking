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
        private readonly object _lock = new();
        private readonly IGrpcServiceConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        private Channel? _channel;
        private string? _currentServiceUrl;

        /// <inheritdoc />
        public Channel? GetOrCreateChannel()
        {
            GrpcChannelTarget? parsed = TryFormatChannelTarget(_configuration.Endpoint());
            if (parsed is null)
            {
                return null;
            }

            GrpcChannelTarget target = parsed.Value;
            lock (_lock)
            {
                if (_channel is null ||
                    _currentServiceUrl != target.ChannelKey ||
                    _channel.State == ChannelState.Shutdown ||
                    _channel.State == ChannelState.TransientFailure)
                {
                    ShutdownChannelInternal();

                    ChannelCredentials credentials = target.UseTransportSecurity
                        ? new SslCredentials()
                        : ChannelCredentials.Insecure;
                    _channel = new Channel(target.Target, credentials);
                    _currentServiceUrl = target.ChannelKey;

                    Trace.WriteLine($"Created new shared gRPC channel to {target.Target} tls={target.UseTransportSecurity}");
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
        /// Stops Grpc.Core native completion-queue threads. Call only after the last channel is shut down
        /// and the process is exiting; Reset() during startup must not call this.
        /// </summary>
        public static void ShutdownEnvironment()
        {
            try
            {
                GrpcEnvironment.ShutdownChannelsAsync().Wait(TimeSpan.FromSeconds(5));
                Trace.WriteLine("gRPC environment shut down successfully");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error shutting down gRPC environment: {ex.Message}");
            }
        }

        /// <summary>
        /// C-core channel targets are host:port[/path][?query]. HTTPS keeps the port and selects TLS;
        /// a missing scheme is treated as plaintext HTTP. Called when the shared channel is created.
        /// </summary>
        internal static GrpcChannelTarget? TryFormatChannelTarget(string rawEndpoint)
        {
            if (string.IsNullOrWhiteSpace(rawEndpoint))
                return null;

            string trimmedEndpoint = rawEndpoint.Trim();
            bool containsScheme = trimmedEndpoint.IndexOf("://", StringComparison.Ordinal) >= 0;
            string endpointToParse = containsScheme ? trimmedEndpoint : $"http://{trimmedEndpoint}";

            if (!Uri.TryCreate(endpointToParse, UriKind.Absolute, out Uri? parsedUri) || parsedUri is null)
                return null;

            if (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)
                return null;

            string absolutePath = parsedUri.AbsolutePath;
            if (string.Equals(absolutePath, "/", StringComparison.Ordinal))
                absolutePath = string.Empty;

            string host = parsedUri.HostNameType == UriHostNameType.IPv6
                ? $"[{parsedUri.Host}]"
                : parsedUri.Host;
            string target = $"{host}:{parsedUri.Port}{absolutePath}{parsedUri.Query}";
            bool useTransportSecurity = parsedUri.Scheme == Uri.UriSchemeHttps;
            return new GrpcChannelTarget(target, useTransportSecurity);
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

    /// <summary>
    /// Host:port target for <see cref="Channel"/> plus whether the original URL was HTTPS.
    /// <see cref="ChannelKey"/> includes the security mode so http and https to the same host do not share a channel.
    /// </summary>
    internal readonly struct GrpcChannelTarget
    {
        public GrpcChannelTarget(string target, bool useTransportSecurity)
        {
            Target = target;
            UseTransportSecurity = useTransportSecurity;
        }

        public string Target { get; }

        public bool UseTransportSecurity { get; }

        public string ChannelKey => $"{(UseTransportSecurity ? "tls" : "plain")}|{Target}";
    }
}

