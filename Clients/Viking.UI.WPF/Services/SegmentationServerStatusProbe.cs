using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Viking.gRPC.SegmentationServiceTypes.V1;

namespace Viking.UI.WPF.Services
{
    /// <summary>Snapshot of one GetServerStatus call, including client-side RTT.</summary>
    public sealed class SegmentationServerProbeResult
    {
        public bool IsReachable { get; set; }
        public double ProbeLatencyMs { get; set; }
        public uint InFlightRequests { get; set; }
        public double RecentLatencyMs { get; set; }
        public uint InferenceWorkers { get; set; }
        public string Version { get; set; }
        public string ServerMessage { get; set; }

        /// <summary><c>ModelInfo.model_id</c>, e.g. "v5". Empty when the server reports no model metadata.</summary>
        public string ModelId { get; set; }

        /// <summary><c>ModelInfo.model_version</c>, e.g. "v5.3" (model 5 after its epoch 3).</summary>
        public string ModelVersion { get; set; }

        public string ModelDisplayName { get; set; }

        /// <summary><c>ModelInfo.training_data_set</c>, e.g. "v3" (a training-set version, numbered apart from models).</summary>
        public string TrainingDataSet { get; set; }

        /// <summary><c>ModelInfo.description</c>: the operator's note on what the model is for.</summary>
        public string ModelDescription { get; set; }

        public uint CachedImages { get; set; }
        public ulong CacheMemoryBytes { get; set; }
    }

    /// <summary>
    /// Calls GetServerStatus so the login picker can rank live SAM2 servers by load and RTT.
    /// </summary>
    public static class SegmentationServerStatusProbe
    {
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

        public static async Task<SegmentationServerProbeResult> ProbeAsync(string endpoint, CancellationToken cancellationToken)
        {
            string target = FormatGrpcTarget(endpoint);
            if (string.IsNullOrWhiteSpace(target))
            {
                return new SegmentationServerProbeResult();
            }

            Channel channel = null;
            try
            {
                channel = CreateChannel(endpoint, target);
                return await CallAsync(channel, endpoint, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SegmentationSelection] Could not open a channel to '{endpoint}': {ex.Message}");
                return new SegmentationServerProbeResult();
            }
            finally
            {
                await ShutdownQuietlyAsync(channel, endpoint).ConfigureAwait(false);
            }
        }

        internal static Channel CreateChannel(string endpoint, string target)
        {
            ChannelCredentials credentials = EndpointRequiresTls(endpoint)
                ? new SslCredentials()
                : ChannelCredentials.Insecure;
            return new Channel(target, credentials);
        }

        /// <summary>One GetServerStatus call on an open channel. Never throws; a failed call is an unreachable result.</summary>
        internal static async Task<SegmentationServerProbeResult> CallAsync(Channel channel, string endpoint, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var client = new SegmentationService.SegmentationServiceClient(channel);
                var deadline = DateTime.UtcNow.Add(ProbeTimeout);
                var call = client.GetServerStatusAsync(
                    new ServerStatusRequest(),
                    deadline: deadline,
                    cancellationToken: cancellationToken);
                var reply = await call.ResponseAsync.ConfigureAwait(false);

                stopwatch.Stop();
                ModelInfo model = reply.Model;
                return new SegmentationServerProbeResult
                {
                    IsReachable = true,
                    ProbeLatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                    InFlightRequests = reply.InFlightRequests,
                    RecentLatencyMs = reply.RecentLatencyMs,
                    InferenceWorkers = reply.InferenceWorkers,
                    Version = reply.Version,
                    ServerMessage = reply.Message,
                    ModelId = model?.ModelId ?? string.Empty,
                    ModelVersion = model?.ModelVersion ?? string.Empty,
                    ModelDisplayName = model?.DisplayName ?? string.Empty,
                    TrainingDataSet = model?.TrainingDataSet ?? string.Empty,
                    ModelDescription = model?.Description ?? string.Empty,
                    CachedImages = reply.CachedImages,
                    CacheMemoryBytes = reply.CacheMemoryBytes
                };
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Trace.WriteLine($"[SegmentationSelection] GetServerStatus failed for '{endpoint}': {ex.Message}");
                }
                return new SegmentationServerProbeResult();
            }
        }

        internal static async Task ShutdownQuietlyAsync(Channel channel, string endpoint)
        {
            if (channel == null)
            {
                return;
            }

            try
            {
                await channel.ShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SegmentationSelection] Channel shutdown failed for '{endpoint}': {ex.Message}");
            }
        }

        /// <summary>
        /// Grpc.Core channels want host:port[/path], not a URI with a scheme.
        /// </summary>
        internal static string FormatGrpcTarget(string rawEndpoint)
        {
            if (string.IsNullOrWhiteSpace(rawEndpoint))
            {
                return null;
            }

            string trimmedEndpoint = rawEndpoint.Trim();
            bool containsScheme = trimmedEndpoint.IndexOf("://", StringComparison.Ordinal) >= 0;
            string endpointToParse = containsScheme ? trimmedEndpoint : "http://" + trimmedEndpoint;

            if (!Uri.TryCreate(endpointToParse, UriKind.Absolute, out Uri parsedUri) || parsedUri is null)
            {
                return null;
            }

            string absolutePath = parsedUri.AbsolutePath;
            if (string.Equals(absolutePath, "/", StringComparison.Ordinal))
            {
                absolutePath = string.Empty;
            }

            string authority = parsedUri.Authority;
            if (string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                parsedUri.Port == 40080)
            {
                // The old cleartext port moved to the TLS port when that listener was removed.
                string host = parsedUri.HostNameType == UriHostNameType.IPv6 ? "[" + parsedUri.Host + "]" : parsedUri.Host;
                authority = host + ":40443";
            }

            return authority + absolutePath + parsedUri.Query;
        }

        /// <summary>
        /// The segmentation server only speaks TLS, so every endpoint that parses uses the system trust store.
        /// </summary>
        internal static bool EndpointRequiresTls(string rawEndpoint)
        {
            if (string.IsNullOrWhiteSpace(rawEndpoint))
            {
                return false;
            }

            string trimmedEndpoint = rawEndpoint.Trim();
            bool containsScheme = trimmedEndpoint.IndexOf("://", StringComparison.Ordinal) >= 0;
            string endpointToParse = containsScheme ? trimmedEndpoint : "http://" + trimmedEndpoint;
            if (!Uri.TryCreate(endpointToParse, UriKind.Absolute, out Uri parsedUri) || parsedUri is null)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Repeats GetServerStatus for the open picker, keeping one channel per server so a refresh every second does not
    /// pay a new TLS handshake each time. A channel whose call fails is closed and opened afresh on the next call, so a
    /// server that comes back is seen at once instead of after gRPC's reconnect back-off.
    /// </summary>
    public sealed class SegmentationServerStatusMonitor : IDisposable
    {
        private readonly object _gate = new object();
        private readonly System.Collections.Generic.Dictionary<string, Channel> _channels =
            new System.Collections.Generic.Dictionary<string, Channel>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public async Task<SegmentationServerProbeResult> ProbeAsync(string endpoint, CancellationToken cancellationToken)
        {
            Channel channel;
            try
            {
                channel = ChannelFor(endpoint);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SegmentationSelection] Could not open a channel to '{endpoint}': {ex.Message}");
                return new SegmentationServerProbeResult();
            }

            if (channel == null)
            {
                return new SegmentationServerProbeResult();
            }

            var result = await SegmentationServerStatusProbe.CallAsync(channel, endpoint, cancellationToken).ConfigureAwait(false);
            if (!result.IsReachable)
            {
                bool removed;
                lock (_gate)
                {
                    removed = _channels.TryGetValue(endpoint, out var current) && ReferenceEquals(current, channel) && _channels.Remove(endpoint);
                }

                if (removed)
                {
                    await SegmentationServerStatusProbe.ShutdownQuietlyAsync(channel, endpoint).ConfigureAwait(false);
                }
            }

            return result;
        }

        private Channel ChannelFor(string endpoint)
        {
            string target = SegmentationServerStatusProbe.FormatGrpcTarget(endpoint);
            if (string.IsNullOrWhiteSpace(target))
            {
                return null;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return null;
                }

                if (!_channels.TryGetValue(endpoint, out var channel))
                {
                    channel = SegmentationServerStatusProbe.CreateChannel(endpoint, target);
                    _channels[endpoint] = channel;
                }

                return channel;
            }
        }

        public void Dispose()
        {
            Channel[] channels;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                channels = new Channel[_channels.Count];
                _channels.Values.CopyTo(channels, 0);
                _channels.Clear();
            }

            foreach (var channel in channels)
            {
                _ = SegmentationServerStatusProbe.ShutdownQuietlyAsync(channel, channel.Target);
            }
        }
    }
}
