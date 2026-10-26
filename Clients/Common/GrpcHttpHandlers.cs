#nullable enable annotations
#if NET
using System;
using System.Net.Http;
using System.Net.Security;
using System.Threading;

namespace Viking.Common
{
    /// <summary>
    /// HTTP handlers for gRPC channels that stay open for hours and carry many concurrent calls. On .NET Framework, gRPC uses
    /// WinHttpHandler (set up by its callers); these are the .NET handlers.
    /// </summary>
    public static class GrpcHttpHandlers
    {
        /// <summary>
        /// A <see cref="SocketsHttpHandler"/> for a long-lived, high-throughput gRPC channel.
        /// <list type="bullet">
        /// <item>More than one HTTP/2 connection is opened when a connection's concurrent-stream limit is reached, so many
        /// simultaneous calls are not queued behind one connection.</item>
        /// <item>Idle connections are never retired by the pool, and keepalive pings run even when no call is active, so a proxy or
        /// NAT does not silently expire an idle stream and a dead connection is noticed within about a minute.</item>
        /// <item>A call that cannot connect fails after <see cref="ConnectTimeout"/> instead of hanging, so the caller's retry or
        /// reconnect logic starts promptly. The channel itself reconnects on the next call.</item>
        /// </list>
        /// </summary>
        /// <param name="remoteCertificateValidation">Optional TLS validation callback; null uses the platform's validation.</param>
        public static SocketsHttpHandler CreateSocketsHttpHandler(RemoteCertificateValidationCallback? remoteCertificateValidation = null)
        {
            var handler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
                KeepAlivePingDelay = KeepAlivePingDelay,
                KeepAlivePingTimeout = KeepAlivePingTimeout,
                ConnectTimeout = ConnectTimeout,
            };

            if (remoteCertificateValidation is not null)
                handler.SslOptions.RemoteCertificateValidationCallback = remoteCertificateValidation;

            return handler;
        }

        /// <summary>Largest gRPC message for channels created without WebAnnotationModel.gRPC's defaults (correction fields exceed the 4 MB default).</summary>
        public const int MaxMessageBytes = 128 * 1024 * 1024;

        /// <summary>How long a connection may be silent before a keepalive ping is sent.</summary>
        public static readonly TimeSpan KeepAlivePingDelay = TimeSpan.FromSeconds(30);

        /// <summary>How long to wait for a ping reply before the connection is considered dead.</summary>
        public static readonly TimeSpan KeepAlivePingTimeout = TimeSpan.FromSeconds(20);

        /// <summary>How long a new connection may take to be established.</summary>
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    }
}
#endif
