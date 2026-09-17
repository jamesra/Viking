using AnnotationVizLib;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Viking.Common;

namespace MonogameTestbed
{
    /// <summary>
    /// Applies <see cref="Program.CommandLineOptions"/> registration correction before SliceGraph.Create.
    /// </summary>
    static class MorphologyRegistration
    {
        public static async Task ApplyAsync(MorphologyGraph graph, Program.CommandLineOptions options)
        {
            if (graph is null || options is null)
                return;

            CorrectionMode mode = options.Correction;
            if (mode == CorrectionMode.None)
            {
                Console.WriteLine("Registration correction: none");
                return;
            }

            double? maxOffsetNm = options.CorrectionMaxOffsetNm;
            int window = options.CorrectionCurveFitWindow > 0
                ? options.CorrectionCurveFitWindow
                : MorphologyGraph.DefaultCurveFitHalfWindow;

            if (mode == CorrectionMode.CurveFit)
            {
                Console.WriteLine($"Registration correction: curvefit (window=±{window}" +
                    (maxOffsetNm is null ? "" : $", maxOffset={maxOffsetNm:F0}nm") + ")");
                using (MeshPhaseTimings.Measure(MeshPhase.SmoothProcesses, graph.Nodes.Count + graph.Subgraphs.Count))
                    MorphologyGraph.CurveFitProcesses(graph, new MorphologyGraph.CurveFitOptions(window, maxOffsetNm, null));
                return;
            }

            // neighbor or all
            Uri neighborEndpoint = options.EndpointUri ?? DataSource.EndpointMap[Endpoint.RC1];
            double radiusNm = options.CorrectionRadiusNm;
            Console.WriteLine($"Registration correction: neighbor (radius={radiusNm:F0} nm)" +
                (mode == CorrectionMode.All ? " then curvefit" : " then curvefit on Sample-null only"));

            NeighborCorrectionResult neighborResult;
            using (MeshPhaseTimings.Measure(MeshPhase.NeighborCorrection))
            {
                MorphologyGraph neighborSources = await AnnotationVizLib.OData.ODataMorphologyFactory
                    .LoadNeighborHopSourcesAsync(graph, neighborEndpoint, radiusNm);
                IEnumerable<MorphologyGraph> extras = neighborSources?.Subgraphs.Values;
                neighborResult = MorphologyGraph.ApplyNeighborCorrection(graph, extras, maxOffsetNm);
            }

            HashSet<ulong> unmovedFilter = BuildUnmovedCurveFitFilter(graph, neighborResult);
            Console.WriteLine($"Curvefit fallback: {unmovedFilter?.Count ?? 0} Sample-null process/terminal ID(s)" +
                (neighborResult.Applied ? "" : " (neighbor skipped; all movable)"));

            using (MeshPhaseTimings.Measure(MeshPhase.SmoothProcesses, graph.Nodes.Count + graph.Subgraphs.Count))
            {
                // neighbor mode: only Sample-null movable; when neighbor skipped, null filter = all movable
                MorphologyGraph.CurveFitOptions unmovedOptions = neighborResult.Applied
                    ? new MorphologyGraph.CurveFitOptions(window, maxOffsetNm, unmovedFilter)
                    : new MorphologyGraph.CurveFitOptions(window, maxOffsetNm, null);
                MorphologyGraph.CurveFitProcesses(graph, unmovedOptions);

                if (mode == CorrectionMode.All)
                {
                    Console.WriteLine($"Registration correction: full curvefit (window=±{window})");
                    MorphologyGraph.CurveFitProcesses(graph, new MorphologyGraph.CurveFitOptions(window, maxOffsetNm, null));
                }
            }
        }

        /// <summary>
        /// Curvefit-movable Location IDs that neighbor did not successfully sample.
        /// </summary>
        static HashSet<ulong> BuildUnmovedCurveFitFilter(MorphologyGraph root, NeighborCorrectionResult neighborResult)
        {
            HashSet<ulong> movable = [];
            CollectCurveFitMovableIds(root, movable);
            if (!neighborResult.Applied)
                return movable;

            movable.ExceptWith(neighborResult.HandledLocationIds);
            return movable;
        }

        static void CollectCurveFitMovableIds(MorphologyGraph graph, HashSet<ulong> into)
        {
            foreach (MorphologyNode node in graph.Nodes.Values)
            {
                if (MorphologyGraph.IsCurveFitMovable(node, graph))
                    into.Add(node.Key);
            }

            foreach (MorphologyGraph subgraph in graph.Subgraphs.Values)
                CollectCurveFitMovableIds(subgraph, into);
        }
    }
}
