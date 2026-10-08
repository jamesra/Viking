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
            Channel? stale = null;
            try
            {
                lock (_lock)
                {
                    if (_channel is null ||
                        _currentServiceUrl != target.ChannelKey ||
                        _channel.State == ChannelState.Shutdown ||
                        _channel.State == ChannelState.TransientFailure)
                    {
                        stale = DetachChannelUnlocked();

                        // The segmentation server no longer has a cleartext listener, so every
                        // channel is TLS whatever scheme the saved endpoint was written with.
                        _channel = new Channel(target.Target, new SslCredentials());
                        _currentServiceUrl = target.ChannelKey;

                        Trace.WriteLine(target.UpgradedFromPlaintext
                            ? $"Created new shared gRPC channel to {target.Target} with TLS (the saved endpoint \"{_configuration.Endpoint()}\" named plaintext, which the server no longer serves)"
                            : $"Created new shared gRPC channel to {target.Target} with TLS");
                    }

                    return _channel;
                }
            }
            finally
            {
                ShutdownDetached(stale, wait: false);
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

        /// <summary>
        /// Drops the shared channel so the next <see cref="GetOrCreateChannel"/> reconnects. The old channel is
        /// detached under the lock and shut down after it is released, without waiting, so a caller on the UI
        /// thread is not held for the shutdown and other threads can create the new channel at once.
        /// Calls already running on the old channel finish or fail with their own cancellation.
        /// </summary>
        public void ResetChannel()
        {
            Channel? stale;
            lock (_lock)
            {
                stale = DetachChannelUnlocked();
                _currentServiceUrl = null;
            }

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
        /// The port the segmentation server published for its cleartext listener before that listener
        /// was removed. A saved endpoint that still names it is moved to <see cref="TlsPort"/>.
        /// </summary>
        internal const int LegacyCleartextPort = 40080;

        /// <summary>The port the segmentation server publishes for gRPC over TLS.</summary>
        internal const int TlsPort = 40443;

        /// <summary>
        /// C-core channel targets are host:port[/path][?query]. The segmentation server only speaks
        /// TLS, so the result always selects TLS. An <c>http://</c> endpoint or one with no scheme was
        /// plaintext before; it keeps its host and port (except <see cref="LegacyCleartextPort"/>,
        /// which becomes <see cref="TlsPort"/>) and is flagged <see cref="GrpcChannelTarget.UpgradedFromPlaintext"/>
        /// so the channel log says what happened. Called when the shared channel is created.
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
            bool upgradedFromPlaintext = parsedUri.Scheme == Uri.UriSchemeHttp;
            int port = upgradedFromPlaintext && parsedUri.Port == LegacyCleartextPort
                ? TlsPort
                : parsedUri.Port;
            string target = $"{host}:{port}{absolutePath}{parsedUri.Query}";
            return new GrpcChannelTarget(target, upgradedFromPlaintext);
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

    /// <summary>
    /// Host:port target for a TLS <see cref="Channel"/>, and whether the saved endpoint named plaintext
    /// (<c>http://</c> or no scheme) and was upgraded.
    /// </summary>
    internal readonly struct GrpcChannelTarget
    {
        public GrpcChannelTarget(string target, bool upgradedFromPlaintext)
        {
            Target = target;
            UpgradedFromPlaintext = upgradedFromPlaintext;
        }

        public string Target { get; }

        public bool UpgradedFromPlaintext { get; }

        public string ChannelKey => $"tls|{Target}";
    }
}

