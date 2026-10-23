using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// Downloads a full published residual field from an AnnotateSectionCorrections URL
    /// (<c>ListCorrectionSets</c> / <c>GetCorrectionManifest</c> / <c>GetSectionCorrection</c>) and samples points
    /// through <c>CorrectPoints</c>. Used by MonogameTestbed when <c>--correction-url</c> is set so mesh runs
    /// consume the same maps the live correction service serves, without reading a local corrections directory.
    /// </summary>
    public sealed class GrpcPublishedCorrectionLoader
    {
        /// <summary>Caps parallel <c>GetSectionCorrection</c> calls so a large volume does not open hundreds of RPCs at once.</summary>
        public const int DefaultSectionDownloadConcurrency = 8;

        readonly Func<Uri, CallInvoker> _invokerFactory;
        readonly int _sectionConcurrency;

        /// <param name="invokerFactory">
        /// Returns the call invoker for a service URL (normally a cached <c>GrpcChannel.CreateCallInvoker</c>).
        /// Called once per RPC batch entry point.
        /// </param>
        /// <param name="sectionDownloadConcurrency">Max concurrent section lattice downloads; non-positive uses <see cref="DefaultSectionDownloadConcurrency"/>.</param>
        public GrpcPublishedCorrectionLoader(Func<Uri, CallInvoker> invokerFactory, int sectionDownloadConcurrency = DefaultSectionDownloadConcurrency)
        {
            _invokerFactory = invokerFactory ?? throw new ArgumentNullException(nameof(invokerFactory));
            _sectionConcurrency = sectionDownloadConcurrency > 0
                ? sectionDownloadConcurrency
                : DefaultSectionDownloadConcurrency;
        }

        /// <summary>
        /// Picks the gRPC <c>volume_name</c> (Identity annotation database) for this host by calling <c>ListVolumes</c>
        /// and matching <paramref name="preferredName"/> and/or the caller's OData/annotation URI. Used when the
        /// client knows which volume it is meshing (-e RC1 / OData URL) but not the service's catalog key.
        /// </summary>
        /// <param name="serviceUrl">Correction service base URL.</param>
        /// <param name="preferredName">
        /// Hint such as an Endpoint enum name (<c>RC1</c>) or an explicit database name; matched case-insensitively
        /// to <c>VolumeInfo.volume_name</c> first.
        /// </param>
        /// <param name="annotationOrODataUri">
        /// The annotation or OData endpoint the morphology was loaded from; matched against each volume's
        /// <c>annotation_endpoint</c> / <c>vikingxml_endpoint</c> and against path segments equal to <c>volume_name</c>.
        /// </param>
        /// <param name="cancellationToken">Cancels <c>ListVolumes</c>.</param>
        /// <returns>The service's <c>volume_name</c>.</returns>
        /// <exception cref="InvalidOperationException">No volume on the service matches the hints.</exception>
        public async Task<string> ResolveVolumeNameAsync(
            Uri serviceUrl,
            string preferredName = null,
            Uri annotationOrODataUri = null,
            CancellationToken cancellationToken = default)
        {
            if (serviceUrl is null)
                throw new ArgumentNullException(nameof(serviceUrl));

            var client = new AnnotateSectionCorrections.AnnotateSectionCorrectionsClient(_invokerFactory(serviceUrl));
            ListVolumesResponse listed = await client
                .ListVolumesAsync(new ListVolumesRequest(), cancellationToken: cancellationToken)
                .ResponseAsync.ConfigureAwait(false);

            string matched = MatchVolumeName(listed.Volumes, preferredName, annotationOrODataUri);
            if (!string.IsNullOrWhiteSpace(matched))
                return matched;

            // Volumes with published sets but missing from ListVolumes (disk-only) still appear in ListCorrectionSets.
            ListCorrectionSetsResponse sets = await client
                .ListCorrectionSetsAsync(new ListCorrectionSetsRequest(), cancellationToken: cancellationToken)
                .ResponseAsync.ConfigureAwait(false);
            matched = MatchVolumeName(
                sets.Sets.Select(s => new VolumeInfo { VolumeName = s.VolumeName }),
                preferredName,
                annotationOrODataUri);
            if (!string.IsNullOrWhiteSpace(matched))
                return matched;

            string hint = DescribeResolveHints(preferredName, annotationOrODataUri);
            throw new InvalidOperationException(
                $"No correction volume on {serviceUrl} matches {hint}. Pass --correction-volume with the annotation database name from ListVolumes.");
        }

        /// <summary>
        /// Chooses a <c>volume_name</c> from catalog rows. Prefer exact name match, then URI host/path overlap,
        /// then a path segment equal to the volume name (e.g. <c>/RC1/OData</c> → <c>RC1</c>).
        /// </summary>
        public static string MatchVolumeName(
            IEnumerable<VolumeInfo> volumes,
            string preferredName,
            Uri annotationOrODataUri)
        {
            List<VolumeInfo> list = volumes?
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.VolumeName))
                .GroupBy(v => v.VolumeName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList() ?? [];
            if (list.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                VolumeInfo byName = list.FirstOrDefault(v =>
                    string.Equals(v.VolumeName, preferredName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                    return byName.VolumeName;
            }

            if (annotationOrODataUri != null)
            {
                string[] pathSegments = PathSegments(annotationOrODataUri);
                VolumeInfo byEndpoint = list.FirstOrDefault(v =>
                    UriOverlaps(annotationOrODataUri, TryParseUri(v.AnnotationEndpoint)) ||
                    UriOverlaps(annotationOrODataUri, TryParseUri(v.VikingxmlEndpoint)));
                if (byEndpoint != null)
                    return byEndpoint.VolumeName;

                VolumeInfo byPath = list
                    .Where(v => pathSegments.Any(seg =>
                        string.Equals(seg, v.VolumeName, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(v => v.VolumeName.Length)
                    .FirstOrDefault();
                if (byPath != null)
                    return byPath.VolumeName;
            }

            return null;
        }

        /// <summary>
        /// Resolves the StosGroup when omitted, then downloads the manifest and every section lattice for that set.
        /// When <paramref name="volumeName"/> is blank, resolves it via <see cref="ResolveVolumeNameAsync"/> using
        /// <paramref name="volumeNameHint"/> and <paramref name="annotationOrODataUri"/>.
        /// </summary>
        /// <param name="serviceUrl">Correction service base URL (Identity <c>CorrectionServiceUrl</c>).</param>
        /// <param name="volumeName">Annotation database / volume key; blank triggers auto-resolve.</param>
        /// <param name="stosGroup">
        /// Published StosGroup; null or blank picks the first set listed for the resolved volume
        /// (ordinal-ignore-case sorted by name).
        /// </param>
        /// <param name="volumeNameHint">Preferred name when auto-resolving (e.g. Endpoint enum <c>RC1</c>).</param>
        /// <param name="annotationOrODataUri">Morphology OData/annotation URI used to match ListVolumes rows.</param>
        /// <param name="cancellationToken">Cancels listing, the manifest read, or in-flight section downloads.</param>
        /// <exception cref="ArgumentException">URL is missing, or volume cannot be resolved and no hint helps.</exception>
        /// <exception cref="InvalidOperationException">No correction set exists for the volume / StosGroup.</exception>
        /// <exception cref="RpcException">Transport or server errors other than per-section NotFound.</exception>
        public async Task<DownloadedCorrectionSet> LoadAsync(
            Uri serviceUrl,
            string volumeName,
            string stosGroup = null,
            string volumeNameHint = null,
            Uri annotationOrODataUri = null,
            CancellationToken cancellationToken = default)
        {
            if (serviceUrl is null)
                throw new ArgumentNullException(nameof(serviceUrl));

            if (string.IsNullOrWhiteSpace(volumeName))
            {
                volumeName = await ResolveVolumeNameAsync(
                    serviceUrl, volumeNameHint, annotationOrODataUri, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(volumeName))
                throw new ArgumentException("Correction volume (annotation database name) is required.", nameof(volumeName));

            var client = new AnnotateSectionCorrections.AnnotateSectionCorrectionsClient(_invokerFactory(serviceUrl));
            string resolvedStos = await ResolveStosGroupAsync(client, volumeName, stosGroup, cancellationToken)
                .ConfigureAwait(false);

            CorrectionManifest manifest = await client
                .GetCorrectionManifestAsync(new GetCorrectionManifestRequest
                {
                    VolumeName = volumeName,
                    StosGroup = resolvedStos
                }, cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);

            double pitch = manifest.Provenance?.PitchNm ?? 0;
            double kernel = manifest.Provenance?.KernelRadiusNm ?? 0;
            if (pitch <= 0)
                pitch = SectionLatticeField.DefaultPitchNm;
            if (kernel <= 0)
                kernel = SectionLatticeField.DefaultKernelRadiusNm;

            long[] zs = manifest.Z.Count > 0
                ? [.. manifest.Z]
                : await ListSectionZsAsync(client, volumeName, resolvedStos, cancellationToken).ConfigureAwait(false);

            ConcurrentDictionary<long, SectionLatticeField> sections = new();
            ConcurrentBag<(int Z, int Gx, int Gy, double Dx, double Dy, int Votes)> nodes = [];

            await ForEachAsync(zs, _sectionConcurrency, async z =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    SectionCorrection response = await client.GetSectionCorrectionAsync(
                        new GetSectionCorrectionRequest
                        {
                            VolumeName = volumeName,
                            StosGroup = resolvedStos,
                            Z = z
                        }, cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);

                    double sectionPitch = response.PitchNm > 0 ? response.PitchNm : pitch;
                    sections[z] = new SectionLatticeField(
                        sectionPitch,
                        kernel,
                        response.Nodes.Select(n => (n.Gx, n.Gy, n.Dx, n.Dy)));

                    foreach (LatticeVector n in response.Nodes)
                    {
                        int votes = n.VoteCount > 0 ? n.VoteCount : 1;
                        nodes.Add(((int)z, n.Gx, n.Gy, n.Dx, n.Dy, votes));
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                {
                    // Manifest Z can list a section whose npz was not published; skip like a disk catalog would.
                }
            }, cancellationToken).ConfigureAwait(false);

            return new DownloadedCorrectionSet(
                volumeName,
                resolvedStos,
                pitch,
                kernel,
                new Dictionary<long, SectionLatticeField>(sections),
                nodes.ToArray());
        }

        /// <summary>
        /// Maps volume XY points through the service's <c>CorrectPoints</c> RPC (Neighbor residual sampling).
        /// Prefer this when validating that a URL-hosted field matches local <see cref="SectionLatticeField.Sample"/>.
        /// </summary>
        public async Task<CorrectPointsResponse> CorrectPointsAsync(
            Uri serviceUrl,
            string volumeName,
            string stosGroup,
            IEnumerable<(long Z, IReadOnlyList<(double X, double Y)> Points)> sections,
            CorrectionMode? mode = null,
            CancellationToken cancellationToken = default)
        {
            if (serviceUrl is null)
                throw new ArgumentNullException(nameof(serviceUrl));
            if (string.IsNullOrWhiteSpace(volumeName))
                throw new ArgumentException("Correction volume is required.", nameof(volumeName));
            if (string.IsNullOrWhiteSpace(stosGroup))
                throw new ArgumentException("StosGroup is required for CorrectPoints.", nameof(stosGroup));
            if (sections is null)
                throw new ArgumentNullException(nameof(sections));

            var client = new AnnotateSectionCorrections.AnnotateSectionCorrectionsClient(_invokerFactory(serviceUrl));
            var request = new CorrectPointsRequest
            {
                VolumeName = volumeName,
                StosGroup = stosGroup
            };
            if (mode.HasValue)
                request.Correction = mode.Value;

            foreach ((long z, IReadOnlyList<(double X, double Y)> points) in sections)
            {
                if (points is null || points.Count == 0)
                    throw new ArgumentException($"Section {z} has no points.", nameof(sections));
                var section = new SectionPoints { Z = z };
                foreach ((double x, double y) in points)
                    section.Points.Add(new VolumeXY { X = x, Y = y });
                request.Sections.Add(section);
            }

            return await client.CorrectPointsAsync(request, cancellationToken: cancellationToken)
                .ResponseAsync.ConfigureAwait(false);
        }

        static string DescribeResolveHints(string preferredName, Uri annotationOrODataUri)
        {
            if (!string.IsNullOrWhiteSpace(preferredName) && annotationOrODataUri != null)
                return $"name '{preferredName.Trim()}' or URI {annotationOrODataUri}";
            if (!string.IsNullOrWhiteSpace(preferredName))
                return $"name '{preferredName.Trim()}'";
            if (annotationOrODataUri != null)
                return $"URI {annotationOrODataUri}";
            return "no name or URI hint";
        }

        static Uri TryParseUri(string text) =>
            Uri.TryCreate(text, UriKind.Absolute, out Uri uri) ? uri : null;

        static string[] PathSegments(Uri uri) =>
            (uri?.AbsolutePath ?? "")
                .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// True when both URIs share a host and at least one non-trivial path segment (ignores trailing
        /// <c>OData</c>/<c>Annotation</c>-style leaf names so OData and WCF roots still match).
        /// </summary>
        static bool UriOverlaps(Uri a, Uri b)
        {
            if (a is null || b is null)
                return false;
            if (!string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            HashSet<string> left = new(PathSegments(a), StringComparer.OrdinalIgnoreCase);
            HashSet<string> right = new(PathSegments(b), StringComparer.OrdinalIgnoreCase);
            left.ExceptWith(TrivialPathLeaves);
            right.ExceptWith(TrivialPathLeaves);
            return left.Overlaps(right);
        }

        static readonly HashSet<string> TrivialPathLeaves = new(StringComparer.OrdinalIgnoreCase)
        {
            "OData", "odata", "Annotation", "ConnectomeData.svc", "Services"
        };

        static async Task<string> ResolveStosGroupAsync(
            AnnotateSectionCorrections.AnnotateSectionCorrectionsClient client,
            string volumeName,
            string stosGroup,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(stosGroup))
                return stosGroup.Trim();

            ListCorrectionSetsResponse listed = await client
                .ListCorrectionSetsAsync(new ListCorrectionSetsRequest { VolumeName = volumeName },
                    cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);

            CorrectionSetInfo chosen = listed.Sets
                .Where(s => string.Equals(s.VolumeName, volumeName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StosGroup, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (chosen is null || string.IsNullOrWhiteSpace(chosen.StosGroup))
            {
                throw new InvalidOperationException(
                    $"No published correction sets for volume '{volumeName}' on the correction service.");
            }

            return chosen.StosGroup;
        }

        static async Task<long[]> ListSectionZsAsync(
            AnnotateSectionCorrections.AnnotateSectionCorrectionsClient client,
            string volumeName,
            string stosGroup,
            CancellationToken cancellationToken)
        {
            ListCorrectedSectionsResponse listed = await client
                .ListCorrectedSectionsAsync(new ListCorrectedSectionsRequest
                {
                    VolumeName = volumeName,
                    StosGroup = stosGroup
                }, cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);
            return [.. listed.Z];
        }

        static async Task ForEachAsync<T>(
            IReadOnlyList<T> items,
            int concurrency,
            Func<T, Task> body,
            CancellationToken cancellationToken)
        {
            if (items.Count == 0)
                return;

            using SemaphoreSlim gate = new(concurrency, concurrency);
            Task[] tasks = new Task[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                tasks[i] = Task.Run(async () =>
                {
                    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await body(item).ConfigureAwait(false);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, cancellationToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
