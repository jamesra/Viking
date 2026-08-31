using AnnotationVizLib;
using Geometry;
using Geometry.JSON;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Slice topology used to be produced by a blocking wait: the caller parked a thread pool thread on a
    /// ManualResetEventSlim while the per-slice tasks it was waiting for were queued behind it on the same pool.
    /// These tests pin the observable contract of the completion-based replacement - identical topology, no hang
    /// on the graph where no task can start, and forward progress when the pool cannot grow.
    /// </summary>
    [TestClass]
    public class TopologySchedulingTests
    {
        const double SectionThickness = 90.0;

        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionThickness, "nm"));

        static (Polygon Lower, Polygon Upper) LoadRc1Pair()
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Testdata", "rc1-structure-1724-adjacent-pair.json");
            Assert.IsTrue(System.IO.File.Exists(path), $"Cached slice pair is missing: {path}");

            using JsonDocument doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            JsonElement root = doc.RootElement;

            return (
                GeometryJSONExtensions.PolygonFromJSON(root.GetProperty("lower").GetRawText()),
                GeometryJSONExtensions.PolygonFromJSON(root.GetProperty("upper").GetRawText()));
        }

        /// <summary>
        /// A two section chain of the cached RC1 contours, which is one slice with two shapes.
        /// </summary>
        static MorphologyGraph BuildRc1PairGraph(ulong structureId = 1724)
        {
            var (lower, upper) = LoadRc1Pair();

            MorphologyGraph graph = new(structureId, TestScale);
            graph.AddNode(new MorphologyNode(1, PolygonLocation(1, lower, 1), graph));
            graph.AddNode(new MorphologyNode(2, PolygonLocation(2, upper, 2), graph));
            graph.AddEdge(new MorphologyEdge(graph, 1, 2));

            return graph;
        }

        static TestLocation PolygonLocation(ulong id, Polygon shape, int section) =>
            new()
            {
                ID = id,
                ParentID = 1,
                UnscaledZ = section,
                Z = section * SectionThickness,
                TypeCode = LocationType.POLYGON,
                VolumeGeometryWKT = ToWkt(shape)
            };

        static string ToWkt(Polygon shape)
        {
            StringBuilder sb = new("POLYGON(");
            sb.Append(RingWkt(shape.ExteriorRing));
            foreach (var interior in shape.InteriorRings)
            {
                sb.Append(", ");
                sb.Append(RingWkt(interior));
            }

            sb.Append(')');
            return sb.ToString();
        }

        static string RingWkt(IReadOnlyList<Vector2> ring) =>
            "(" + string.Join(", ", ring.Select(p => string.Format(CultureInfo.InvariantCulture, "{0} {1}", p.X, p.Y))) + ")";

        /// <summary>Slice key mapped to the number of shapes in that slice's topology.</summary>
        static SortedDictionary<ulong, int> TopologyShapeCounts(SliceGraph slices)
        {
            SortedDictionary<ulong, int> counts = [];
            foreach (ulong key in slices.Nodes.Keys)
                counts.Add(key, slices.GetTopology(key).Shapes?.Length ?? 0);

            return counts;
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task SliceTopology_Rc1Pair_IsUnchangedAcrossRuns()
        {
            SliceGraph first = await SliceGraph.Create(BuildRc1PairGraph(), 2.0);
            SliceGraph second = await SliceGraph.Create(BuildRc1PairGraph(), 2.0);

            var firstCounts = TopologyShapeCounts(first);
            var secondCounts = TopologyShapeCounts(second);

            Assert.AreEqual(1, firstCounts.Count, "The cached RC1 pair is a single slice.");
            Assert.AreEqual(2, firstCounts.Values.Single(), "The slice holds the lower and upper contour.");

            CollectionAssert.AreEqual(firstCounts.Keys.ToArray(), secondCounts.Keys.ToArray(), "Slice keys differ between runs.");
            CollectionAssert.AreEqual(firstCounts.Values.ToArray(), secondCounts.Values.ToArray(), "Shape counts per slice differ between runs.");
        }

        /// <summary>
        /// The single annotation structure is the case the old implementation had to special case: it only waited
        /// when at least one task had started.  The completion source has to preserve that, or a graph where no
        /// task can start never completes.
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task SliceTopology_SingleAnnotation_Completes()
        {
            MorphologyGraph graph = new(2, TestScale);
            graph.AddNode(new MorphologyNode(1, PolygonLocation(1, Square(0, 0, 100), 1), graph));

            SliceGraph slices = await SliceGraph.Create(graph, 2.0);

            foreach (ulong key in slices.Nodes.Keys)
                Assert.IsNotNull(slices.GetTopology(key).Shapes, $"Slice {key} has no topology.");
        }

        /// <summary>
        /// Forward progress with a thread pool that cannot grow.
        ///
        /// The pool is capped at its current thread count and more structures than that are meshed at once.  A
        /// caller that blocks a pool thread waiting on work queued behind it on the same pool cannot finish here
        /// at all, so completion within the timeout is the assertion.
        /// </summary>
        [TestMethod]
        [Timeout(120000)]
        public async Task SliceTopology_DoesNotConsumePoolThreadWhileWaiting()
        {
            System.Threading.ThreadPool.GetMaxThreads(out int maxWorker, out int maxIO);
            System.Threading.ThreadPool.GetMinThreads(out int minWorker, out int minIO);

            int cap = Math.Max(Environment.ProcessorCount, minWorker);
            int structures = (cap * 4) + 8;

            Assert.IsTrue(System.Threading.ThreadPool.SetMaxThreads(cap, maxIO), "Could not cap the worker thread pool.");
            try
            {
                var graphs = Enumerable.Range(0, structures)
                                       .Select(i => BuildRc1PairGraph((ulong)(3000 + i)))
                                       .ToArray();

                SliceGraph[] results = await Task.WhenAll(graphs.Select(g => SliceGraph.Create(g, 2.0)));

                foreach (SliceGraph slices in results)
                    Assert.AreEqual(1, TopologyShapeCounts(slices).Count);
            }
            finally
            {
                System.Threading.ThreadPool.SetMaxThreads(maxWorker, maxIO);
                System.Threading.ThreadPool.SetMinThreads(minWorker, minIO);
            }
        }

        static Polygon Square(double cx, double cy, double half) =>
            new(
            [
                new Vector2(cx - half, cy - half),
                new Vector2(cx + half, cy - half),
                new Vector2(cx + half, cy + half),
                new Vector2(cx - half, cy + half),
                new Vector2(cx - half, cy - half),
            ]);
    }
}
