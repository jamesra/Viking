using AnnotationVizLib;
using Geometry;
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
    /// Synthetic tests for leave-one-out residual admission and spatial pooling of
    /// <see cref="NeighborResidualField"/>. Does not hit OData.
    /// </summary>
    [TestClass]
    public class NeighborResidualFieldTests
    {
        const double HalfWidth = 50.0;
        const double SectionZ = 90.0;

        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionZ, "nm"));

        [TestMethod]
        public void AsymmetricAdmission_BothSidesWithTwoEach_Admits()
        {
            // 5 nodes: index 2 has 2 below and 2 above.
            Vector2[] centroids =
            [
                new(0, 0), new(10, 0), new(100, 50), new(30, 0), new(40, 0)
            ];
            double[] z = [90, 180, 270, 360, 450];

            Vector2? predicted = MorphologyGraph.TryEvaluateAsymmetricLeaveOneOut(
                centroids, z, 2, MorphologyGraph.ResidualWindowOptions.Default);

            Assert.IsNotNull(predicted);
            // Leave-one-out through a mostly-horizontal chain should pull the outlier toward Y≈0.
            Assert.IsTrue(Math.Abs(predicted.Value.Y) < Math.Abs(centroids[2].Y),
                $"Predicted Y ({predicted.Value.Y}) should be closer to the chain than actual ({centroids[2].Y})");
        }

        [TestMethod]
        public void AsymmetricAdmission_BothSidesButOnlyOneBelow_Drops()
        {
            // Index 1 has 1 below and 3 above — not enough for minBoth=2 on each side,
            // and not one-sided, so sample is dropped.
            Vector2[] centroids =
            [
                new(0, 0), new(10, 50), new(20, 0), new(30, 0), new(40, 0)
            ];
            double[] z = [90, 180, 270, 360, 450];

            Vector2? predicted = MorphologyGraph.TryEvaluateAsymmetricLeaveOneOut(
                centroids, z, 1, MorphologyGraph.ResidualWindowOptions.Default);

            Assert.IsNull(predicted);
        }

        [TestMethod]
        public void AsymmetricAdmission_OneSidedWithFiveAbove_Admits()
        {
            // Terminal at index 0 with 5 neighbors above.
            Vector2[] centroids =
            [
                new(100, 80), new(10, 0), new(20, 0), new(30, 0), new(40, 0), new(50, 0)
            ];
            double[] z = [90, 180, 270, 360, 450, 540];

            Vector2? predicted = MorphologyGraph.TryEvaluateAsymmetricLeaveOneOut(
                centroids, z, 0, MorphologyGraph.ResidualWindowOptions.Default);

            Assert.IsNotNull(predicted);
        }

        [TestMethod]
        public void AsymmetricAdmission_OneSidedWithFewerThanMinOne_Drops()
        {
            // Terminal with only 3 neighbors above — below MinOne=5.
            Vector2[] centroids =
            [
                new(100, 80), new(10, 0), new(20, 0), new(30, 0)
            ];
            double[] z = [90, 180, 270, 360];

            Vector2? predicted = MorphologyGraph.TryEvaluateAsymmetricLeaveOneOut(
                centroids, z, 0, MorphologyGraph.ResidualWindowOptions.Default);

            Assert.IsNull(predicted);
        }

        [TestMethod]
        public void CollectResiduals_DropsShortOrBranchOnlyChains()
        {
            MorphologyGraph shortChain = BuildCell(1,
            [
                (1, 0, 0, 1),
                (2, 0, 0, 2),
            ], LinkSequential(1, 2));

            List<NeighborResidualField.ResidualSample> residuals =
                NeighborResidualField.CollectResiduals([shortChain]);

            Assert.AreEqual(0, residuals.Count, "Length < 3 process should produce no residuals");
        }

        [TestMethod]
        public void CollectResiduals_JitteredMidShaftProducesNonZeroResidual()
        {
            MorphologyGraph cell = BuildCell(1,
            [
                (1, 0, 0, 1),
                (2, 10, 0, 2),
                (3, 20, 80, 3), // outlier
                (4, 30, 0, 4),
                (5, 40, 0, 5),
            ], LinkSequential(1, 5));

            List<NeighborResidualField.ResidualSample> residuals =
                NeighborResidualField.CollectResiduals([cell]);

            NeighborResidualField.ResidualSample mid = residuals.Single(r => r.LocationID == 3);
            Assert.AreEqual(3, mid.SectionZ);
            Assert.IsTrue(Math.Abs(mid.DY) > 1.0, $"Expected non-trivial DY residual, got {mid.DY}");
            // predicted ≈ Y=0, actual Y=80 → residual ≈ -80
            Assert.IsTrue(mid.DY < 0, "Residual should pull toward the curve (negative DY)");
            Assert.IsFalse(mid.OneSided);
        }

        [TestMethod]
        public void Build_ThreeAdmittedAnnotationsFromOneStructure_CanVote()
        {
            // One cell with three parallel process chains, all jittered the same way on section 3.
            // Each admitted annotation is an independent observation of registration.
            MorphologyGraph cell = BuildMultiProcessCell(structureId: 1, processCount: 3, jitterY: 80);

            List<NeighborResidualField.ResidualSample> residuals =
                NeighborResidualField.CollectResiduals([cell]);
            Assert.IsTrue(residuals.Count >= 3, "Fixture should produce multiple residuals from one cell");

            NeighborResidualField field = NeighborResidualField.Build(residuals);
            Assert.IsTrue(field.CellCount > 0,
                "Three admitted annotations should satisfy MinAnnotationVotes even within one structure");

            Vector2? correction = field.Sample(new Vector2(5020, 0), sectionZ: 3);
            Assert.IsNotNull(correction);
            Assert.IsTrue(correction.Value.Y < -1.0,
                $"Expected negative DY correction, got {correction.Value.Y}");
        }

        [TestMethod]
        public void Build_ThreeStructures_PoolsAndSamples()
        {
            List<MorphologyGraph> cells =
            [
                BuildJitteredCell(1, baseX: 0, jitterY: 80),
                BuildJitteredCell(2, baseX: 200, jitterY: 80),
                BuildJitteredCell(3, baseX: 400, jitterY: 80),
            ];

            List<NeighborResidualField.ResidualSample> residuals =
                NeighborResidualField.CollectResiduals(cells);
            Assert.IsTrue(residuals.Count >= 3);

            NeighborResidualField field = NeighborResidualField.Build(residuals);
            Assert.IsTrue(field.CellCount > 0, "Three independent structures should pool");

            // Sample near cell 2's mid-shaft (section 3, X≈220).
            Vector2? correction = field.Sample(new Vector2(220, 0), sectionZ: 3);
            Assert.IsNotNull(correction, "Kernel should cover mid-section near the cells");
            // Consensus residual should pull Y toward 0 (negative DY).
            Assert.IsTrue(correction.Value.Y < -1.0,
                $"Expected negative DY correction, got {correction.Value.Y}");
        }

        [TestMethod]
        public void SampleAlways_OutsideSupport_IsIdentityUntrusted()
        {
            NeighborResidualField empty = NeighborResidualField.FromLattice([]);
            (Vector2 offset, bool trusted) = empty.SampleAlways(new Vector2(0, 0), 1);
            Assert.IsFalse(trusted);
            Assert.AreEqual(0, offset.X, 1e-9);
            Assert.AreEqual(0, offset.Y, 1e-9);
        }

        [TestMethod]
        public void SampleAlways_OccupiedCell_IsBilinearTrusted()
        {
            NeighborResidualField field = NeighborResidualField.FromLattice(
            [
                (1, 0, 0, new Vector2(10, -4), 3),
                (1, 1, 0, new Vector2(10, -4), 3),
                (1, 0, 1, new Vector2(10, -4), 3),
                (1, 1, 1, new Vector2(10, -4), 3)
            ]);
            (Vector2 offset, bool trusted) = field.SampleAlways(new Vector2(1000, 1000), 1);
            Assert.IsTrue(trusted);
            Assert.AreEqual(10, offset.X, 1e-6);
            Assert.AreEqual(-4, offset.Y, 1e-6);
            Assert.IsNotNull(field.Sample(new Vector2(1000, 1000), 1));
        }

        [TestMethod]
        public void ApplyNeighborCorrection_TranslatesTowardCurve()
        {
            // Each neighbor contributes one admitted annotation after target-structure leave-one-out.
            MorphologyGraph root = new(0, TestScale);
            MorphologyGraph a = BuildJitteredCell(1, baseX: 0, jitterY: 80);
            MorphologyGraph b = BuildJitteredCell(2, baseX: 200, jitterY: 80);
            MorphologyGraph c = BuildJitteredCell(3, baseX: 400, jitterY: 80);
            MorphologyGraph d = BuildJitteredCell(4, baseX: 600, jitterY: 80);
            root.AddSubgraph(a);
            root.AddSubgraph(b);
            root.AddSubgraph(c);
            root.AddSubgraph(d);

            Vector2 before = a.Nodes[3].Center.XY();
            NeighborCorrectionResult result = MorphologyGraph.ApplyNeighborCorrection(root);
            Assert.IsTrue(result.Applied);
            Assert.IsTrue(result.HandledLocationIds.Contains(3),
                $"Expected location 3 handled; got [{string.Join(",", result.HandledLocationIds)}]");

            Vector2 after = a.Nodes[3].Center.XY();
            Assert.IsTrue(Math.Abs(after.Y) < Math.Abs(before.Y),
                $"Node should move toward Y=0 ({before.Y} -> {after.Y})");
        }

        [TestMethod]
        public void CurveFitProcesses_StillUsesSymmetricHalfWindow()
        {
            // Regression: exposing the asymmetric path must not change CurveFitProcesses behavior.
            MorphologyGraph graph = BuildJitteredCell(1, baseX: 0, jitterY: 15);
            Dictionary<ulong, Vector2> before = graph.Nodes.Values.ToDictionary(n => n.Key, n => n.Center.XY());

            MorphologyGraph.CurveFitProcesses(graph);

            double rmsBefore = Math.Sqrt(before.Where(kv => kv.Key is >= 2 and <= 4)
                .Average(kv => kv.Value.Y * kv.Value.Y));
            double rmsAfter = Math.Sqrt(graph.Nodes.Values.Where(n => n.Key is >= 2 and <= 4)
                .Average(n => n.Center.Y * n.Center.Y));
            Assert.IsTrue(rmsAfter < rmsBefore, $"CurveFitProcesses should still smooth ({rmsBefore} -> {rmsAfter})");
        }

        static MorphologyGraph BuildJitteredCell(ulong structureId, double baseX, double jitterY)
        {
            return BuildCell(structureId,
            [
                (1, baseX, 0, 1),
                (2, baseX + 10, 0, 2),
                (3, baseX + 20, jitterY, 3),
                (4, baseX + 30, 0, 4),
                (5, baseX + 40, 0, 5),
            ], LinkSequential(1, 5));
        }

        static MorphologyGraph BuildMultiProcessCell(ulong structureId, int processCount, double jitterY)
        {
            MorphologyGraph graph = new(structureId, TestScale);
            ulong nextId = 1;
            for (int p = 0; p < processCount; p++)
            {
                double baseX = p * 5000; // Far apart so they do not share a 2 µm square by home bin alone.
                ulong start = nextId;
                for (int s = 1; s <= 5; s++)
                {
                    double y = s == 3 ? jitterY : 0;
                    graph.AddNode(new MorphologyNode(nextId, SquareLocation(nextId, structureId, baseX + s * 10, y, s), graph));
                    nextId++;
                }

                for (ulong i = start; i < nextId - 1; i++)
                    graph.AddEdge(new MorphologyEdge(graph, i, i + 1));
            }

            return graph;
        }

        static MorphologyGraph BuildCell(ulong structureId, (ulong id, double x, double y, int section)[] nodes, (ulong a, ulong b)[] links)
        {
            MorphologyGraph graph = new(structureId, TestScale);
            foreach ((ulong id, double x, double y, int section) in nodes)
                graph.AddNode(new MorphologyNode(id, SquareLocation(id, structureId, x, y, section), graph));
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

        static TestLocation SquareLocation(ulong id, ulong parentId, double cx, double cy, int section)
        {
            double z = section * SectionZ;
            return new TestLocation
            {
                ID = id,
                ParentID = parentId,
                UnscaledZ = section,
                Z = z,
                TypeCode = LocationType.POLYGON,
                VolumeGeometryWKT = string.Format(CultureInfo.InvariantCulture,
                    "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                    cx - HalfWidth, cy - HalfWidth, cx + HalfWidth, cy + HalfWidth)
            };
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
