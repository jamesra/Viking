using AnnotationVizLib;
using Geometry;
using Grpc.Core;
using MorphologyMesh;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Viking.Common;
using Viking.SectionCorrection;
using Viking.SectionCorrectionClient;

namespace MonogameTestbed
{
    /// <summary>
    /// Runs the <see cref="CorrectionMode"/> flags on a morphology graph before SliceGraph.Create.
    /// Each flag is an independent fix; <see cref="CorrectionMode.All"/> (default) enables every fix, in order.
    /// Residual fields prefer a live correction-service URL, then a local <c>--corrections-dir</c>, then OData neighbors.
    /// </summary>
    static class MorphologyRegistration
    {
        static readonly ConcurrentDictionary<Uri, GrpcChannel> CorrectionChannels = new();

        public static PublishedCorrectionSet LoadedPublishedSet { get; private set; }

        /// <summary>Field downloaded from <c>--correction-url</c>, when that path was used.</summary>
        public static DownloadedCorrectionSet LoadedServiceSet { get; private set; }

        /// <summary>
        /// Apply enabled registration fixes. Neighbor is the spatial residual field; CurveFit is a later
        /// outlier pass for folds/tears the field cannot represent. They do not fill each other's holes.
        /// </summary>
        public static async Task ApplyAsync(MorphologyGraph graph, Program.CommandLineOptions options)
        {
            if (graph is null || options is null)
                return;

            CorrectionMode mode = options.Correction;
            if ((mode & ~CorrectionMode.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(options), mode, "Unknown CorrectionMode flag.");

            if (mode == CorrectionMode.None)
            {
                Console.WriteLine("Registration correction: none");
                await TryLoadPublishedOrServiceSetAsync(options).ConfigureAwait(false);
                return;
            }

            Console.WriteLine($"Registration correction: {FormatMode(mode)}");

            if (mode.HasFlag(CorrectionMode.Neighbor))
                await ApplyResidualFieldAsync(graph, options).ConfigureAwait(false);
            else
                await TryLoadPublishedOrServiceSetAsync(options).ConfigureAwait(false);

            if (mode.HasFlag(CorrectionMode.CurveFit))
                ApplyOutlierCurveFit(graph, options);
        }

        /// <summary>
        /// Loads a published or service field for overlay / inspection without applying Neighbor correction.
        /// </summary>
        public static async Task<bool> TryLoadPublishedOrServiceSetAsync(Program.CommandLineOptions options)
        {
            if (await TryLoadServiceSetAsync(options).ConfigureAwait(false))
                return true;
            return TryLoadPublishedSet(options);
        }

        public static bool TryLoadPublishedSet(Program.CommandLineOptions options)
        {
            if (LoadedPublishedSet != null)
                return true;
            if (options is null || string.IsNullOrWhiteSpace(options.CorrectionsDirectory))
                return false;
            if (!Directory.Exists(options.CorrectionsDirectory))
            {
                Console.WriteLine($"Published corrections directory not found: {options.CorrectionsDirectory}");
                return false;
            }

            using CorrectionCatalog catalog = new(options.CorrectionsDirectory);
            catalog.Load();
            PublishedCorrectionSet set = null;
            if (!string.IsNullOrWhiteSpace(options.CorrectionStosGroup))
            {
                if (!catalog.TryGet(options.CorrectionStosGroup, out set))
                    throw new InvalidOperationException($"No published correction set '{options.CorrectionStosGroup}' under {options.CorrectionsDirectory}.");
            }
            else
            {
                set = catalog.Sets.OrderBy(s => s.StosGroup, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (set is null)
                {
                    Console.WriteLine($"No published correction sets under {options.CorrectionsDirectory}");
                    return false;
                }
            }

            LoadedPublishedSet = set;
            Console.WriteLine($"Loaded published residual field {set.StosGroup} ({set.Sections.Count} sections) from {options.CorrectionsDirectory}");
            return true;
        }

        static async Task<bool> TryLoadServiceSetAsync(Program.CommandLineOptions options)
        {
            if (LoadedServiceSet != null)
                return true;
            if (options is null)
                return false;

            if (string.IsNullOrWhiteSpace(options.CorrectionServiceUrl))
            {
                LogCorrectionService(
                    "Correction service omitted (--correction-url empty). " +
                    "Residual field will use --corrections-dir if set, otherwise OData neighbor rebuild.");
                return false;
            }

            if (!Uri.TryCreate(options.CorrectionServiceUrl.Trim(), UriKind.Absolute, out Uri serviceUrl))
            {
                LogCorrectionService(
                    $"Correction service cannot be used: invalid --correction-url '{options.CorrectionServiceUrl}'.");
                throw new ArgumentException($"Invalid --correction-url '{options.CorrectionServiceUrl}'.");
            }

            string explicitVolume = string.IsNullOrWhiteSpace(options.CorrectionVolume)
                ? null
                : options.CorrectionVolume.Trim();
            // EndpointUri already maps -e RC1 → OData URL, or accepts a raw OData URI.
            string volumeHint = VolumeNameHint(options);
            Uri morphologyUri = options.EndpointUri;

            var loader = new GrpcPublishedCorrectionLoader(uri =>
                CorrectionChannels.GetOrAdd(uri, u => GrpcChannel.ForAddress(u, new GrpcChannelOptions
                {
                    HttpHandler = GrpcHttpHandlers.CreateSocketsHttpHandler(),
                    MaxReceiveMessageSize = GrpcHttpHandlers.MaxMessageBytes,
                    MaxSendMessageSize = GrpcHttpHandlers.MaxMessageBytes,
                })).CreateCallInvoker());

            LogCorrectionService(
                $"Downloading residual field from {serviceUrl}" +
                (explicitVolume != null
                    ? $" volume={explicitVolume}"
                    : $" (auto volume from ListVolumes; hint={volumeHint ?? "none"}, uri={morphologyUri?.AbsoluteUri ?? "none"})") +
                (string.IsNullOrWhiteSpace(options.CorrectionStosGroup)
                    ? " (first StosGroup)"
                    : $" stos={options.CorrectionStosGroup}"));

            try
            {
                DownloadedCorrectionSet set = await loader
                    .LoadAsync(
                        serviceUrl,
                        explicitVolume,
                        options.CorrectionStosGroup,
                        volumeNameHint: volumeHint,
                        annotationOrODataUri: morphologyUri)
                    .ConfigureAwait(false);

                if (!set.HasSections)
                {
                    LogCorrectionService(
                        $"Correction service cannot be used: no section lattices for '{set.VolumeName}' '{set.StosGroup}' at {serviceUrl}. " +
                        "Falling back to --corrections-dir or OData neighbors.");
                    return false;
                }

                LoadedServiceSet = set;
                LogCorrectionService(
                    $"Loaded service residual field {set.VolumeName}/{set.StosGroup} ({set.Sections.Count} sections, pitch={set.PitchNm:F0}nm) from {serviceUrl}");
                return true;
            }
            catch (Exception ex) when (ex is RpcException or InvalidOperationException or HttpRequestException or IOException or TimeoutException)
            {
                LogCorrectionService(
                    $"Correction service cannot be used ({serviceUrl}): {ex.GetType().Name}: {ex.Message}. " +
                    "Falling back to --corrections-dir or OData neighbors.");
                return false;
            }
        }

        /// <summary>
        /// Console plus <see cref="Trace"/> so <c>-l</c>/<c>-v</c> capture correction-service availability.
        /// </summary>
        static void LogCorrectionService(string message)
        {
            Console.WriteLine(message);
            Trace.WriteLine(message);
        }

        /// <summary>
        /// Name hint for gRPC volume auto-resolve: explicit <c>--correction-volume</c>, else the -e Endpoint enum name
        /// (<c>RC1</c>, …). The real <c>volume_name</c> is confirmed against the service's <c>ListVolumes</c> catalog.
        /// </summary>
        public static string VolumeNameHint(Program.CommandLineOptions options)
        {
            if (options is null)
                return null;
            if (!string.IsNullOrWhiteSpace(options.CorrectionVolume))
                return options.CorrectionVolume.Trim();

            if (!string.IsNullOrWhiteSpace(options.EndpointParam))
            {
                try
                {
                    return options.EndpointParam.ToEnum<Endpoint>().ToString();
                }
                catch
                {
                    // Custom OData URL: ListVolumes matches the URI path instead.
                }
            }

            return null;
        }

        static NeighborResidualField ToNeighborField(PublishedCorrectionSet set)
        {
            double pitch = set.Provenance?.PitchNm > 0 ? set.Provenance.PitchNm : NeighborResidualField.GridSizeNm;
            double kernel = set.Provenance?.KernelRadiusNm > 0 ? set.Provenance.KernelRadiusNm : NeighborResidualField.KernelRadiusNm;
            return NeighborResidualField.FromLattice(set.EnumerateLattice(), pitch, kernel);
        }

        static NeighborResidualField ToNeighborField(DownloadedCorrectionSet set)
        {
            IEnumerable<(int Z, int Gx, int Gy, Vector2 Offset, int Votes)> nodes = set.EnumerateLatticeNodes()
                .Select(n => (n.Z, n.Gx, n.Gy, new Vector2(n.Dx, n.Dy), n.Votes));
            return NeighborResidualField.FromLattice(nodes, set.PitchNm, set.KernelRadiusNm);
        }

        /// <summary>
        /// Spatial mean displacement field from neighboring process curve-residuals (predicted − actual).
        /// Corrects the smoothly varying leftover registration after Stos. Skipped when the neighbor
        /// corpus is too small; that is not a reason to run CurveFit unless that flag is also set.
        /// </summary>
        static async Task ApplyResidualFieldAsync(MorphologyGraph graph, Program.CommandLineOptions options)
        {
            if (await TryLoadServiceSetAsync(options).ConfigureAwait(false) && LoadedServiceSet != null)
            {
                LogCorrectionService(
                    $"Residual field: correction service {LoadedServiceSet.VolumeName}/{LoadedServiceSet.StosGroup}");
                using (MeshPhaseTimings.Measure(MeshPhase.NeighborCorrection))
                {
                    MorphologyGraph.ApplyResidualField(
                        graph,
                        ToNeighborField(LoadedServiceSet),
                        options.CorrectionMaxOffsetNm);
                }
                return;
            }

            if (TryLoadPublishedSet(options) && LoadedPublishedSet != null)
            {
                LogCorrectionService(
                    $"Residual field: published directory {LoadedPublishedSet.StosGroup} (correction service not used).");
                using (MeshPhaseTimings.Measure(MeshPhase.NeighborCorrection))
                {
                    MorphologyGraph.ApplyResidualField(
                        graph,
                        ToNeighborField(LoadedPublishedSet),
                        options.CorrectionMaxOffsetNm);
                }
                return;
            }

            Uri neighborEndpoint = options.EndpointUri ?? DataSource.EndpointMap[Endpoint.RC1];
            double radiusNm = options.CorrectionRadiusNm;
            LogCorrectionService(
                $"Residual field: OData neighbor rebuild radius={radiusNm:F0} nm at {neighborEndpoint} " +
                "(correction service not used).");

            using (MeshPhaseTimings.Measure(MeshPhase.NeighborCorrection))
            {
                MorphologyGraph neighborSources = await AnnotationVizLib.OData.ODataMorphologyFactory
                    .LoadNeighborHopSourcesAsync(graph, neighborEndpoint, radiusNm)
                    .ConfigureAwait(false);
                MorphologyGraph.ApplyNeighborCorrection(
                    graph,
                    neighborSources?.Subgraphs.Values,
                    options.CorrectionMaxOffsetNm);
            }
        }

        /// <summary>
        /// Leave-one-out Catmull-Rom on this structure's processes. Recovers large outliers the residual
        /// field is systematically wrong about (section fold, tear, a contour far from the local consensus).
        /// Always a full pass over movable locations — not a fallback for unsampled field points.
        /// </summary>
        static void ApplyOutlierCurveFit(MorphologyGraph graph, Program.CommandLineOptions options)
        {
            int window = options.CorrectionCurveFitWindow > 0
                ? options.CorrectionCurveFitWindow
                : MorphologyGraph.DefaultCurveFitHalfWindow;
            double? maxOffsetNm = options.CorrectionMaxOffsetNm;
            Console.WriteLine($"Outlier curvefit: window=±{window}" +
                (maxOffsetNm is null ? "" : $", maxOffset={maxOffsetNm:F0}nm"));

            using (MeshPhaseTimings.Measure(MeshPhase.SmoothProcesses, graph.Nodes.Count + graph.Subgraphs.Count))
                MorphologyGraph.CurveFitProcesses(graph, new MorphologyGraph.CurveFitOptions(window, maxOffsetNm, null));
        }

        static string FormatMode(CorrectionMode mode)
        {
            if (mode == CorrectionMode.All)
                return "everything (residual field, then outlier curvefit)";
            if (mode == CorrectionMode.Neighbor)
                return "residual field only";
            if (mode == CorrectionMode.CurveFit)
                return "outlier curvefit only";
            return mode.ToString();
        }
    }
}
