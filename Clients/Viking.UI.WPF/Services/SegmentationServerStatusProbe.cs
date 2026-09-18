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
            var stopwatch = Stopwatch.StartNew();
            try
            {
                channel = new Channel(target, ChannelCredentials.Insecure);
                var client = new SegmentationService.SegmentationServiceClient(channel);
                var deadline = DateTime.UtcNow.Add(ProbeTimeout);
                var call = client.GetServerStatusAsync(
                    new ServerStatusRequest(),
                    deadline: deadline,
                    cancellationToken: cancellationToken);
                var reply = await call.ResponseAsync.ConfigureAwait(false);

                stopwatch.Stop();
                return new SegmentationServerProbeResult
                {
                    IsReachable = true,
                    ProbeLatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                    InFlightRequests = reply.InFlightRequests,
                    RecentLatencyMs = reply.RecentLatencyMs,
                    InferenceWorkers = reply.InferenceWorkers,
                    Version = reply.Version,
                    ServerMessage = reply.Message
                };
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SegmentationSelection] GetServerStatus failed for '{endpoint}': {ex.Message}");
                return new SegmentationServerProbeResult();
            }
            finally
            {
                if (channel != null)
                {
                    try
                    {
                        await channel.ShutdownAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[SegmentationSelection] Channel shutdown failed for '{endpoint}': {ex.Message}");
                    }
                }
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

            return parsedUri.Authority + absolutePath + parsedUri.Query;
        }
    }
}
