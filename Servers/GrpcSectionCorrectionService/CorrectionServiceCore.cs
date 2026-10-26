using AnnotationVizLib;
using Geometry;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;
using FitMode = AnnotationVizLib.CorrectionMode;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// The work behind every protocol version of AnnotateSectionCorrections, in plain types.  Each version's service
    /// class only translates its messages to and from these calls, so retiring a version deletes translation code
    /// alone.  Failures are <see cref="RpcException"/>s, whose status codes every version shares.
    /// </summary>
    public sealed class CorrectionServiceCore
    {
        readonly VolumeCorrectionCatalog _catalog;
        readonly IIdentityVolumeSource _volumes;
        readonly AnnotationConnectionResolver _connections;
        readonly IConfiguration _configuration;
        readonly RebuildStatusStore _rebuild;
        readonly ILogger _logger;
        readonly CorrectionRebuildRunner _rebuildRunner;
        readonly IHostApplicationLifetime _lifetime;

        public CorrectionServiceCore(
            VolumeCorrectionCatalog catalog,
            IIdentityVolumeSource volumes,
            AnnotationConnectionResolver connections,
            IConfiguration configuration,
            RebuildStatusStore rebuild,
            ILogger logger,
            CorrectionRebuildRunner rebuildRunner = null,
            IHostApplicationLifetime lifetime = null)
        {
            _catalog = catalog;
            _volumes = volumes;
            _connections = connections;
            _configuration = configuration;
            _rebuild = rebuild;
            _logger = logger;
            _rebuildRunner = rebuildRunner;
            _lifetime = lifetime;
        }

        public sealed record VolumeEntry(string Name, string VikingXmlEndpoint, string AnnotationEndpoint);

        public sealed record CorrectedLocationData(long LocationId, long StructureId, long Z, Vector2 Position, Vector2 Offset);

        /// <summary>
        /// A CorrectStructures answer.  <see cref="FieldBuiltUtc"/> is the sampled set's build time, or null when the
        /// mode does not sample a field or the set is missing.
        /// </summary>
        public sealed record StructuresAnswer(
            IReadOnlyList<CorrectedLocationData> Locations, StructureFingerprint Fingerprint, DateTime? FieldBuiltUtc);

        /// <summary>One sampled section: <see cref="Found"/> is false when the set has no map there.</summary>
        public sealed record SectionSample(bool Found, IReadOnlyList<(Vector2 Offset, bool Trusted)> Points);

        /// <summary>
        /// Identity contexts that have an annotation database, one per database (the correction-set key), sorted.
        /// Identity transport failures are UNAVAILABLE.
        /// </summary>
        public async Task<IReadOnlyList<VolumeEntry>> ListVolumesAsync(CancellationToken cancellationToken)
        {
            Dictionary<string, VolumeEntry> byName = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (IdentityVolumeRow row in await _volumes.ListAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!row.HasAnnotationDatabase)
                        continue;
                    byName[row.CorrectionDatabase] =
                        new VolumeEntry(row.CorrectionDatabase, row.VikingXmlEndpoint ?? "", row.AnnotationEndpoint ?? "");
                }
            }
            catch (Exception ex) when (IsIdentityTransportFailure(ex, cancellationToken))
            {
                _logger.LogWarning(ex, "Identity volume list failed.");
                throw new RpcException(new Status(StatusCode.Unavailable, $"Identity volume list unavailable: {ex.Message}"));
            }

            return [.. byName.Values.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)];
        }

        public RebuildSnapshot RebuildStatus() => _rebuild.Snapshot();

        /// <summary>
        /// Manual rebuild trigger for the container's correction-menu console.  The gRPC surface is otherwise
        /// anonymous, so this only accepts a caller on the loopback interface (a process inside the container);
        /// traffic published through Docker arrives from the bridge address and is refused.
        /// </summary>
        public (bool Started, string Message) StartRebuild(ServerCallContext context, string volumeName, bool force)
        {
            IPAddress caller = context.GetHttpContext()?.Connection.RemoteIpAddress;
            if (caller is null || !IPAddress.IsLoopback(caller))
                throw new RpcException(new Status(StatusCode.PermissionDenied, "RebuildVolume is only available from inside the service host."));
            if (_rebuildRunner is null)
                throw new RpcException(new Status(StatusCode.Unimplemented, "This host has no rebuild runner."));

            string volume = volumeName?.Trim() ?? "";
            bool started = _rebuildRunner.TryStartInBackground(volume, force, _lifetime?.ApplicationStopping ?? CancellationToken.None);
            string target = volume.Length == 0 ? "all volumes" : $"'{volume}'";
            _logger.LogInformation("Manual rebuild of {Target} (force={Force}) {Result}.",
                target, force, started ? "started" : "refused: one is already running");
            return (started, started ? $"Rebuild of {target} started." : "A rebuild is already running.");
        }

        /// <summary>Published sets, sorted by volume then StosGroup; an empty filter lists every volume.</summary>
        public IEnumerable<(string VolumeName, PublishedCorrectionSet Set)> Sets(string volumeFilter)
        {
            IEnumerable<(string VolumeName, PublishedCorrectionSet Set)> sets = _catalog.Sets;
            if (!string.IsNullOrWhiteSpace(volumeFilter))
                sets = sets.Where(s => string.Equals(s.VolumeName, volumeFilter, StringComparison.OrdinalIgnoreCase));

            return sets
                .OrderBy(s => s.VolumeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Set.StosGroup, StringComparer.OrdinalIgnoreCase);
        }

        public PublishedCorrectionSet RequireSet(string volumeName, string stosGroup)
        {
            if (string.IsNullOrWhiteSpace(volumeName) ||
                string.IsNullOrWhiteSpace(stosGroup) ||
                !_catalog.TryGet(volumeName, stosGroup, out PublishedCorrectionSet set))
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"no correction set '{volumeName}' '{stosGroup}'"));
            }

            return set;
        }

        public SectionVectorField RequireSection(string volumeName, string stosGroup, long z)
        {
            PublishedCorrectionSet set = RequireSet(volumeName, stosGroup);
            if (!set.TryGetSection(z, out SectionVectorField field))
            {
                throw new RpcException(new Status(
                    StatusCode.NotFound, $"no correction for section {z} in '{volumeName}' '{stosGroup}'"));
            }

            return field;
        }

        /// <summary>
        /// Samples the published field at caller-held points, one result per section in request order.  A section the set
        /// has no map for reports not found with zero, untrusted offsets.
        /// </summary>
        public IReadOnlyList<SectionSample> SamplePoints(
            string volumeName,
            string stosGroup,
            IReadOnlyList<(long Z, IReadOnlyList<Vector2> Points)> sections)
        {
            if (sections is null || sections.Count == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "sections is empty"));

            HashSet<long> seen = [];
            foreach ((long z, IReadOnlyList<Vector2> points) in sections)
            {
                if (points is null || points.Count == 0)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"section {z} has no points"));
                if (!seen.Add(z))
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"duplicate z {z}"));
            }

            PublishedCorrectionSet set = RequireSet(volumeName, stosGroup);
            List<SectionSample> result = new(sections.Count);
            foreach ((long z, IReadOnlyList<Vector2> points) in sections)
            {
                if (!set.TryGetSection(z, out SectionVectorField field))
                {
                    result.Add(new SectionSample(false, [.. points.Select(_ => (Vector2.Zero, false))]));
                    continue;
                }

                result.Add(new SectionSample(true, [.. points.Select(p => field.Sample(p))]));
            }

            return result;
        }

        /// <summary>
        /// Loads the structures (plus children when asked) mapped through the StosGroup, then applies
        /// <paramref name="mode"/>: the published field if it includes Neighbor, then curvefit if it includes CurveFit.
        /// </summary>
        public async Task<StructuresAnswer> CorrectStructuresAsync(
            string volumeName,
            string stosGroup,
            IReadOnlyList<long> structureIds,
            bool includeChildren,
            FitMode mode,
            CancellationToken cancellationToken)
        {
            string connection = RequireConnection(volumeName, structureIds);

            string volumeUrl = await VolumeUrlAsync(volumeName, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(volumeUrl))
                throw new RpcException(new Status(StatusCode.NotFound, $"no VikingXML endpoint for '{volumeName}'"));

            SqlMorphologyLoader.StructuresLoadResult loaded;
            try
            {
                loaded = await SqlMorphologyLoader.LoadStructuresAsync(
                    connection,
                    volumeUrl,
                    stosGroup,
                    structureIds,
                    includeChildren,
                    _configuration["Corrections:CachePath"],
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransientVolumeFailure(ex, cancellationToken))
            {
                // Volume XML / stos HTTP load shares the host with rebuild traffic; map transient load
                // failures to UNAVAILABLE so clients can wait rather than treat UNKNOWN as a hard fault.
                // The stos/section loads fault tasks, so the failure usually arrives wrapped in an
                // AggregateException (Task.Result) and the volume host refusing a connection is a SocketException.
                _logger.LogWarning(ex, "Volume load failed for '{Volume}'.", volumeName);
                throw new RpcException(new Status(
                    StatusCode.Unavailable, $"volume mapping temporarily unavailable for '{volumeName}': {ex.Message}"));
            }
            catch (Exception ex) when (IsSqlTimeout(ex))
            {
                throw SqlTimeout(ex, volumeName);
            }

            PublishedCorrectionSet published = SampledSet(volumeName, stosGroup, mode);
            NeighborResidualField field = published is null ? null : ToNeighborField(published);

            List<CorrectedLocationData> locations = [];
            foreach (MorphologyGraph cell in loaded.Cells)
            {
                Dictionary<ulong, Vector2> before = [];
                foreach (MorphologyNode node in cell.Nodes.Values)
                    before[node.Key] = new Vector2(node.Center.X, node.Center.Y);

                if (mode.HasFlag(FitMode.Neighbor))
                    MorphologyGraph.ApplyResidualField(cell, field);
                if (mode.HasFlag(FitMode.CurveFit))
                    MorphologyGraph.CurveFitProcesses(cell, new MorphologyGraph.CurveFitOptions(
                        MorphologyGraph.DefaultCurveFitHalfWindow, null, null));

                foreach (MorphologyNode node in cell.Nodes.Values)
                {
                    Vector2 corrected = new(node.Center.X, node.Center.Y);
                    locations.Add(new CorrectedLocationData(
                        (long)node.Key, (long)cell.StructureID, node.Location.UnscaledZ, corrected, corrected - before[node.Key]));
                }
            }

            return new StructuresAnswer(locations, loaded.Fingerprint, published?.Provenance?.BuiltUtc);
        }

        /// <summary>
        /// The parts of a CorrectStructures answer's stamp that change with data, from SQL aggregates and the catalog
        /// only: no volume load, no fit.
        /// </summary>
        public async Task<(StructureFingerprint Fingerprint, DateTime? FieldBuiltUtc)> AnswerStampAsync(
            string volumeName,
            string stosGroup,
            IReadOnlyList<long> structureIds,
            bool includeChildren,
            FitMode mode,
            CancellationToken cancellationToken)
        {
            StructureFingerprint fingerprint =
                await FingerprintAsync(volumeName, structureIds, includeChildren, cancellationToken).ConfigureAwait(false);
            return (fingerprint, SampledSet(volumeName, stosGroup, mode)?.Provenance?.BuiltUtc);
        }

        /// <summary>The structures' current annotation fingerprint from SQL aggregates alone.</summary>
        public async Task<StructureFingerprint> FingerprintAsync(
            string volumeName, IReadOnlyList<long> structureIds, bool includeChildren, CancellationToken cancellationToken)
        {
            string connection = RequireConnection(volumeName, structureIds);
            try
            {
                return await SqlMorphologyLoader.LoadStructureFingerprintAsync(
                    connection, structureIds, includeChildren, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsSqlTimeout(ex))
            {
                throw SqlTimeout(ex, volumeName);
            }
        }

        /// <summary>The set CorrectStructures samples under <paramref name="mode"/>, or null (no Neighbor, or no set).</summary>
        PublishedCorrectionSet SampledSet(string volumeName, string stosGroup, FitMode mode) =>
            mode.HasFlag(FitMode.Neighbor) && _catalog.TryGet(volumeName, stosGroup, out PublishedCorrectionSet set) ? set : null;

        string RequireConnection(string volumeName, IReadOnlyList<long> structureIds)
        {
            if (structureIds is null || structureIds.Count == 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "structure_ids is empty"));

            string connection = _connections.Resolve(volumeName);
            if (string.IsNullOrWhiteSpace(connection))
                throw new RpcException(new Status(StatusCode.FailedPrecondition, $"no annotation SQL mapping for '{volumeName}'"));
            return connection;
        }

        RpcException SqlTimeout(Exception ex, string volumeName)
        {
            // A slow or blocked annotation database is transient; UNAVAILABLE lets clients retry and keep
            // cached offsets instead of holding UNKNOWN as a hard fault.
            _logger.LogWarning(ex, "Annotation SQL timeout loading structures for '{Volume}'.", volumeName);
            return new RpcException(new Status(StatusCode.Unavailable, $"annotation database timed out for '{volumeName}'; retry shortly"));
        }

        /// <summary>
        /// The VikingXML endpoint Identity lists for <paramref name="volumeName"/>, or "" when it is not listed.
        /// Identity transport failures are UNAVAILABLE, like the volume-load failure beside it; caller cancellation
        /// is rethrown unchanged.
        /// </summary>
        async Task<string> VolumeUrlAsync(string volumeName, CancellationToken cancellationToken)
        {
            try
            {
                foreach (IdentityVolumeRow row in await _volumes.ListAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (string.Equals(row.Name, volumeName, StringComparison.OrdinalIgnoreCase))
                        return row.VikingXmlEndpoint;
                }

                return "";
            }
            catch (Exception ex) when (IsIdentityTransportFailure(ex, cancellationToken))
            {
                _logger.LogWarning(ex, "Identity volume list failed for CorrectStructures '{Volume}'.", volumeName);
                throw new RpcException(new Status(
                    StatusCode.Unavailable, $"Identity volume list temporarily unavailable for '{volumeName}': {ex.Message}"));
            }
        }

        static NeighborResidualField ToNeighborField(PublishedCorrectionSet set)
        {
            double pitch = set.Provenance?.PitchNm > 0 ? set.Provenance.PitchNm : NeighborResidualField.GridSizeNm;
            double kernel = set.Provenance?.KernelRadiusNm > 0 ? set.Provenance.KernelRadiusNm : NeighborResidualField.KernelRadiusNm;
            return NeighborResidualField.FromLattice(set.EnumerateLattice(), pitch, kernel);
        }

        /// <summary>
        /// True when a volume load failed because the volume host (or the network to it) is unreachable or slow:
        /// <see cref="WebException"/>, <see cref="HttpRequestException"/>, <see cref="SocketException"/>,
        /// <see cref="IOException"/>, <see cref="ObjectDisposedException"/>, or an HTTP timeout
        /// (<see cref="OperationCanceledException"/> while <paramref name="callCancellation"/> is not cancelled),
        /// found at the top level or anywhere in the <see cref="Exception.InnerException"/> /
        /// <see cref="AggregateException"/> tree. Caller cancellation is excluded so it surfaces as CANCELLED.
        /// </summary>
        public static bool IsTransientVolumeFailure(Exception ex, CancellationToken callCancellation)
        {
            if (ex is null)
                return false;
            if (ex is WebException or HttpRequestException or SocketException or IOException or ObjectDisposedException)
                return true;
            if (ex is OperationCanceledException)
                return !callCancellation.IsCancellationRequested;
            if (ex is AggregateException aggregate)
                return aggregate.InnerExceptions.Any(e => IsTransientVolumeFailure(e, callCancellation));
            return IsTransientVolumeFailure(ex.InnerException, callCancellation);
        }

        /// <summary>
        /// True when <paramref name="ex"/> (or an inner exception) is a SQL Server command timeout
        /// (<c>SqlException.Number</c> -2, "Execution Timeout Expired").
        /// </summary>
        internal static bool IsSqlTimeout(Exception ex)
        {
            for (Exception e = ex; e is not null; e = e.InnerException)
            {
                if (e is Microsoft.Data.SqlClient.SqlException sql && sql.Number == -2)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True for failures of an <see cref="IIdentityVolumeSource"/> that mean Identity is unreachable rather than
        /// a bug: HTTP and I/O errors, the missing-token <see cref="InvalidOperationException"/> from
        /// <see cref="IdentityAssignmentClient"/>, the not-configured error from <see cref="UnconfiguredIdentityVolumeSource"/>,
        /// and an HTTP timeout (an <see cref="OperationCanceledException"/> while <paramref name="callCancellation"/> is
        /// not cancelled).  Caller cancellation is excluded so it surfaces as CANCELLED.
        /// </summary>
        static bool IsIdentityTransportFailure(Exception ex, CancellationToken callCancellation) => ex switch
        {
            OperationCanceledException => !callCancellation.IsCancellationRequested,
            HttpRequestException or IOException or InvalidOperationException => true,
            _ => false
        };

        /// <summary>UTC protobuf timestamp; an unspecified kind is taken as UTC.</summary>
        public static Timestamp ToTimestamp(DateTime value)
        {
            DateTime utc = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
            return Timestamp.FromDateTime(utc);
        }

        public static Timestamp ToTimestamp(DateTime? value) => value is DateTime v ? ToTimestamp(v) : null;
    }
}
