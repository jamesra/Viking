using AnnotationVizLib;
using MorphologyMesh;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Viking.Common;
using Viking.SectionCorrection;

namespace MonogameTestbed
{
    /// <summary>
    /// Runs the <see cref="CorrectionMode"/> flags on a morphology graph before SliceGraph.Create.
    /// Each flag is an independent fix; <see cref="CorrectionMode.All"/> (default) enables every fix, in order.
    /// </summary>
    static class MorphologyRegistration
    {
        public static PublishedCorrectionSet LoadedPublishedSet { get; private set; }

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
                TryLoadPublishedSet(options);
                return;
            }

            Console.WriteLine($"Registration correction: {FormatMode(mode)}");

            if (mode.HasFlag(CorrectionMode.Neighbor))
                await ApplyResidualFieldAsync(graph, options).ConfigureAwait(false);
            else
                TryLoadPublishedSet(options);

            if (mode.HasFlag(CorrectionMode.CurveFit))
                ApplyOutlierCurveFit(graph, options);
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

        static NeighborResidualField ToNeighborField(PublishedCorrectionSet set)
        {
            double pitch = set.Provenance?.PitchNm > 0 ? set.Provenance.PitchNm : NeighborResidualField.GridSizeNm;
            double kernel = set.Provenance?.KernelRadiusNm > 0 ? set.Provenance.KernelRadiusNm : NeighborResidualField.KernelRadiusNm;
            return NeighborResidualField.FromLattice(set.EnumerateLattice(), pitch, kernel);
        }

        /// <summary>
        /// Spatial mean displacement field from neighboring process curve-residuals (predicted − actual).
        /// Corrects the smoothly varying leftover registration after Stos. Skipped when the neighbor
        /// corpus is too small; that is not a reason to run CurveFit unless that flag is also set.
        /// </summary>
        static async Task ApplyResidualFieldAsync(MorphologyGraph graph, Program.CommandLineOptions options)
        {
            if (TryLoadPublishedSet(options) && LoadedPublishedSet != null)
            {
                Console.WriteLine($"Residual field: published {LoadedPublishedSet.StosGroup}");
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
            Console.WriteLine($"Residual field: neighbor radius={radiusNm:F0} nm");

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
