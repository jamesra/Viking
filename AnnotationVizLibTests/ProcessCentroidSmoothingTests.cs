using AnnotationVizLib;
using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationVizLibTests
{
    /// <summary>
    /// Synthetic MorphologyGraph cases for <see cref="MorphologyGraph.CurveFitProcesses"/>.
    /// Does not hit OData; locations are axis-aligned squares with known centroids.
    /// </summary>
    [TestClass]
    public class ProcessCentroidSmoothingTests
    {
        const double HalfWidth = 100.0;
        const double SectionZ = 90.0;

        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionZ, "nm"));

        [TestMethod]
        public void JitteredUnbranchedChain_ProcessAndTerminalCentroidsMoveCloserToLine_BranchesPinned()
        {
            MorphologyGraph graph = BuildChain(
            [
                (1, 0, 0, 1),
                (2, 15, 0, 2),
                (3, -15, 0, 3),
                (4, 15, 0, 4),
                (5, -15, 0, 5),
                (6, 15, 0, 6),
                (7, 0, 0, 7),
            ], LinkSequential(1, 7));

            Dictionary<ulong, Vector2> before = CaptureCentroids(graph);
            Dictionary<ulong, Vector2[]> relBefore = CaptureRelativeVerts(graph);

            MorphologyGraph.CurveFitProcesses(graph);

            double rmsBefore = RmsX(before, 2, 3, 4, 5, 6);
            double rmsAfter = RmsX(CaptureCentroids(graph), 2, 3, 4, 5, 6);
            Assert.IsTrue(rmsAfter < rmsBefore, $"Process RMS X should drop ({rmsBefore} -> {rmsAfter})");

            AssertRelativeVertsUnchanged(graph, relBefore);
        }

        [TestMethod]
        public void SameSectionBranch_CentroidUnchanged()
        {
            // 10-11-12 shaft into branch 13 that links to two partners on the next section.
            MorphologyGraph graph = BuildChain(
            [
                (10, 0, 0, 1),
                (11, 20, 0, 2),
                (12, 4, 0, 3),
                (13, 8, 0, 4),
                (14, 8, 12, 5),
                (15, 8, -12, 5),
            ],
            [
                (10, 11), (11, 12), (12, 13), (13, 14), (13, 15)
            ]);

            Assert.IsTrue(graph.Nodes[13].IsSameSectionBranch());
            Assert.IsFalse(graph.Nodes[13].IsUnbranchedProcess());
            Assert.IsTrue(graph.Nodes[11].IsUnbranchedProcess());
            Assert.IsFalse(MorphologyGraph.IsCurveFitMovable(graph.Nodes[13], graph));

            Vector2 branchBefore = graph.Nodes[13].Center.XY();

            MorphologyGraph.CurveFitProcesses(graph);

            Assert.AreEqual(branchBefore.X, graph.Nodes[13].Center.X, Tolerance.Epsilon);
            Assert.AreEqual(branchBefore.Y, graph.Nodes[13].Center.Y, Tolerance.Epsilon);
        }

        [TestMethod]
        public void SubgraphOnProcessNode_CoMovesWithParent()
        {
            MorphologyGraph graph = BuildChain(
            [
                (1, 0, 0, 1),
                (2, 18, 0, 2),
                (3, -18, 0, 3),
                (4, 18, 0, 4),
                (5, 0, 0, 5),
            ], LinkSequential(1, 5));

            MorphologyGraph synapse = new(99, TestScale);
            synapse.AddNode(new MorphologyNode(99, SquareLocation(99, 18, 1, 2), synapse));
            graph.AddSubgraph(synapse);

            Assert.IsTrue(graph.Nodes[2].Subgraphs.Any(s => s.StructureID == 99), "Synapse should attach to the jittered process node");

            Vector2 parentBefore = graph.Nodes[2].Center.XY();
            Vector2 childBefore = synapse.Nodes[99].Center.XY();

            MorphologyGraph.CurveFitProcesses(graph);

            Vector2 parentDelta = graph.Nodes[2].Center.XY() - parentBefore;
            Vector2 childDelta = synapse.Nodes[99].Center.XY() - childBefore;
            Assert.AreEqual(parentDelta.X, childDelta.X, 1e-6);
            Assert.AreEqual(parentDelta.Y, childDelta.Y, 1e-6);
            Assert.IsTrue(parentDelta.Magnitude > Tolerance.Epsilon, "Process node 2 should have translated");
        }

        [TestMethod]
        public void SubgraphOnTerminal_CoMovesWhenTerminalMoves()
        {
            MorphologyGraph graph = BuildChain(
            [
                (1, 40, 0, 1),
                (2, 18, 0, 2),
                (3, -18, 0, 3),
                (4, 0, 0, 4),
            ], LinkSequential(1, 4));

            MorphologyGraph synapse = new(50, TestScale);
            synapse.AddNode(new MorphologyNode(50, SquareLocation(50, 40, 1, 1), synapse));
            graph.AddSubgraph(synapse);

            Assert.IsTrue(graph.Nodes[1].Subgraphs.Any(s => s.StructureID == 50));
            Assert.IsTrue(MorphologyGraph.IsCurveFitMovable(graph.Nodes[1], graph));

            Vector2 terminalBefore = graph.Nodes[1].Center.XY();
            Vector2 childBefore = synapse.Nodes[50].Center.XY();

            MorphologyGraph.CurveFitProcesses(graph);

            Vector2 terminalDelta = graph.Nodes[1].Center.XY() - terminalBefore;
            Vector2 childDelta = synapse.Nodes[50].Center.XY() - childBefore;
            Assert.AreEqual(terminalDelta.X, childDelta.X, 1e-6);
            Assert.AreEqual(terminalDelta.Y, childDelta.Y, 1e-6);
        }

        [TestMethod]
        public void OnlyLocationIdsFilter_SkipsOtherMovableNodes()
        {
            MorphologyGraph graph = BuildChain(
            [
                (1, 0, 0, 1),
                (2, 20, 0, 2),
                (3, -20, 0, 3),
                (4, 20, 0, 4),
                (5, 0, 0, 5),
            ], LinkSequential(1, 5));

            Vector2 before3 = graph.Nodes[3].Center.XY();
            MorphologyGraph.CurveFitProcesses(graph, new MorphologyGraph.CurveFitOptions(7, null, new HashSet<ulong> { 2 }));

            Assert.AreEqual(before3.X, graph.Nodes[3].Center.X, Tolerance.Epsilon, "Node 3 not in filter must stay");
            Assert.IsTrue(Math.Abs(graph.Nodes[2].Center.X) < Math.Abs(before3.X) || graph.Nodes[2].Center.X != 20,
                "Node 2 in filter should be eligible to move");
        }

        [TestMethod]
        public void ResidualOrder_LargestOutlierCorrectedTowardLine()
        {
            // Nodes 2–6 sit on X=0; node 4 is a large outlier so it has the worst leave-one-out residual.
            MorphologyGraph graph = BuildChain(
            [
                (1, 0, 0, 1),
                (2, 0, 0, 2),
                (3, 0, 0, 3),
                (4, 80, 0, 4),
                (5, 0, 0, 5),
                (6, 0, 0, 6),
                (7, 0, 0, 7),
            ], LinkSequential(1, 7));

            MorphologyGraph.CurveFitProcesses(graph);

            Assert.IsTrue(Math.Abs(graph.Nodes[4].Center.X) < 20,
                $"Worst residual node should peel toward the line (X={graph.Nodes[4].Center.X})");
        }

        [TestMethod]
        public void CurveFitOptions_HalfWindowDefaultsAndClamps()
        {
            Assert.AreEqual(7, MorphologyGraph.DefaultCurveFitHalfWindow);
            Assert.AreEqual(7, MorphologyGraph.CurveFitOptions.Default.HalfWindow);
            Assert.AreEqual(1, new MorphologyGraph.CurveFitOptions(0, null, null).HalfWindow);
            Assert.AreEqual(4, new MorphologyGraph.CurveFitOptions(4, 25, null).HalfWindow);
            Assert.AreEqual(25, new MorphologyGraph.CurveFitOptions(4, 25, null).MaxOffsetNm);
            Assert.IsNull(new MorphologyGraph.CurveFitOptions(4, null, null).MaxOffsetNm);
        }

        [TestMethod]
        public void SameSectionOverlap_BlocksCollidingTranslation()
        {
            // Node 2 would be pulled toward X=250; an unlinked same-section square at X=250 would collide
            // (half-width 100) unless LimitOffsetToAvoidSameSectionOverlap refuses/shrinks the move.
            MorphologyGraph graph = BuildChain(
            [
                (1, 0, 0, 1),
                (2, 0, 0, 2),
                (3, 250, 0, 3),
            ], LinkSequential(1, 3));
            graph.AddNode(new MorphologyNode(99, SquareLocation(99, 250, 0, 2), graph));

            Assert.IsFalse(graph.Nodes[2].Geometry.STIntersects(graph.Nodes[99].Geometry).IsTrue,
                "Fixture: squares must start disjoint");

            MorphologyGraph.CurveFitProcesses(graph, new MorphologyGraph.CurveFitOptions(7, null, new HashSet<ulong> { 2 }));

            Assert.IsFalse(graph.Nodes[2].Geometry.STIntersects(graph.Nodes[99].Geometry).IsTrue,
                "Curvefit must not create a new same-section intersection");
            Assert.IsTrue(graph.Nodes[2].Center.X < 150,
                $"Blocked move should leave node 2 short of colliding with 99 (X={graph.Nodes[2].Center.X})");
        }

        [TestMethod]
        public void Processes_IncludesPinnedEndpoints()
        {
            MorphologyGraph graph = BuildChain(
            [
                (1, 0, 0, 1),
                (2, 0, 0, 2),
                (3, 0, 0, 3),
                (4, 0, 0, 4),
            ], LinkSequential(1, 4));

            List<ulong[]> processes = graph.Processes();
            Assert.AreEqual(1, processes.Count);
            CollectionAssert.AreEqual(new ulong[] { 1, 2, 3, 4 }, processes[0]);
            Assert.IsTrue(graph.Nodes[1].IsProcessTerminal());
            Assert.IsTrue(graph.Nodes[4].IsProcessTerminal());
            Assert.IsTrue(graph.Nodes[2].IsUnbranchedProcess());
        }

        static MorphologyGraph BuildChain((ulong id, double x, double y, int section)[] nodes, (ulong a, ulong b)[] links)
        {
            MorphologyGraph graph = new(1, TestScale);
            foreach ((ulong id, double x, double y, int section) in nodes)
                graph.AddNode(new MorphologyNode(id, SquareLocation(id, x, y, section), graph));
            foreach ((ulong a, ulong b) in links)
                graph.AddEdge(new MorphologyEdge(graph, a, b));
            return graph;
        }

        static (ulong a, ulong b)[] LinkSequential(ulong first, ulong last)
        {
            (ulong a, ulong b)[] links = new (ulong, ulong)[last - first];
            for (ulong i = first; i < last; i++)
                links[i - first] = (i, i + 1);
            return links;
        }

        static TestLocation SquareLocation(ulong id, double cx, double cy, int section)
        {
            double z = section * SectionZ;
            return new TestLocation
            {
                ID = id,
                ParentID = 1,
                UnscaledZ = section,
                Z = z,
                TypeCode = LocationType.POLYGON,
                VolumeGeometryWKT = SquareWkt(cx, cy, HalfWidth)
            };
        }

        static string SquareWkt(double cx, double cy, double half) =>
            string.Format(CultureInfo.InvariantCulture,
                "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                cx - half, cy - half, cx + half, cy + half);

        static Dictionary<ulong, Vector2> CaptureCentroids(MorphologyGraph graph) =>
            graph.Nodes.Values.ToDictionary(n => n.Key, n => n.Center.XY());

        static Dictionary<ulong, Vector2[]> CaptureRelativeVerts(MorphologyGraph graph) =>
            graph.Nodes.Values.ToDictionary(n => n.Key, RelativeVerts);

        static Vector2[] RelativeVerts(MorphologyNode n)
        {
            Vector2 c = n.Center.XY();
            SqlGeometry g = n.Geometry;
            int count = (int)g.STNumPoints().Value;
            Vector2[] rel = new Vector2[count];
            for (int i = 1; i <= count; i++)
            {
                SqlGeometry p = g.STPointN(i);
                rel[i - 1] = new Vector2(p.STX.Value, p.STY.Value) - c;
            }

            return rel;
        }

        static void AssertRelativeVertsUnchanged(MorphologyGraph graph, Dictionary<ulong, Vector2[]> before)
        {
            foreach (MorphologyNode n in graph.Nodes.Values)
            {
                Vector2[] after = RelativeVerts(n);
                Vector2[] expected = before[n.Key];
                Assert.AreEqual(expected.Length, after.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.AreEqual(expected[i].X, after[i].X, 0.05, $"Node {n.Key} vertex {i} X relative to centroid");
                    Assert.AreEqual(expected[i].Y, after[i].Y, 0.05, $"Node {n.Key} vertex {i} Y relative to centroid");
                }
            }
        }

        static double RmsX(Dictionary<ulong, Vector2> centroids, params ulong[] ids)
        {
            double sum = ids.Sum(id => centroids[id].X * centroids[id].X);
            return Math.Sqrt(sum / ids.Length);
        }

        sealed class TestLocation : ILocationReadOnly
        {
            public ulong ID { get; init; }
            public ulong ParentID { get; init; }
            public bool Terminal { get; init; }
            public bool OffEdge { get; init; }
            public bool IsVericosityCap { get; init; }
            public bool IsUntraceable { get; init; }
            public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
            public long UnscaledZ { get; init; }
            public LocationType TypeCode { get; init; }
            public double Z { get; init; }
            public double? Width { get; init; }
            public string MosaicGeometryWKT { get; init; }
            public string VolumeGeometryWKT { get; init; }

            public bool Equals(ILocationReadOnly other) => other != null && ID == other.ID;
        }
    }
}
