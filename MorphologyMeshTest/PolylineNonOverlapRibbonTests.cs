using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMeshTest
{
    /// <summary>
    /// RPC1 structure 53730 / locations 381857–381868: two short OPENCURVE segments on adjacent
    /// sections that do not XY-overlap. Child structures (gap junctions, rafts, synapses) often look
    /// like this — not the stacked-horizontal ribbons PolylineRibbonTests already cover.
    /// </summary>
    [TestClass]
    public class PolylineNonOverlapRibbonTests
    {
        /// <summary>Location 381857 (section 747), volume XY as returned by OData.</summary>
        static readonly Polyline Upper381857 = new(
        [
            new Vector2(34493.436288000004, 34174.563072000004),
            new Vector2(34756.88714451192, 34096.74946242044),
        ]);

        /// <summary>Location 381868 (section 746), volume XY as returned by OData.</summary>
        static readonly Polyline Lower381868 = new(
        [
            new Vector2(34174.332416000005, 34318.183424),
            new Vector2(34545.80541214743, 34132.19484385257),
        ]);

        const double LowerZ = 52220.0;
        const double UpperZ = 52290.0;
        const double SectionThickness = 70.0;

        static BajajGeneratorMesh MeshFromPair(Polyline lower, Polyline upper, bool[,] linked = null)
        {
            if (linked is null)
            {
                linked = new bool[2, 2];
                linked[0, 0] = linked[1, 1] = linked[0, 1] = linked[1, 0] = true;
            }

            // Mirror SliceGraph: densify short open curves, virtual overlap, correspondence, topology.
            IShape2D[] shapes =
            [
                PolylineRibbonMeshGenerator.PrepareOpenPolyline(new Polyline(PolylinePoints(lower))),
                PolylineRibbonMeshGenerator.PrepareOpenPolyline(new Polyline(PolylinePoints(upper))),
            ];
            bool[] isUpper = [false, true];
            Vector2[] offsets = SliceTopology.TryTranslateNonOverlappingShapes(shapes, isUpper, linked);

            var corresponding = new System.Collections.Generic.List<IShape2D>(shapes).AddCorrespondingVertices();
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(
                [.. shapes.OfType<Polyline>()], corresponding);

            SliceTopology topology = new(
                shapes,
                isUpper,
                [LowerZ, UpperZ],
                shapeIndexToMorphNodeIndex: [381868UL, 381857UL],
                sliceThickness: SectionThickness,
                virtualOverlapOffsets: offsets,
                shapesAreLinked: linked,
                buildForkPartition: true,
                shapeLocationTypes: [Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE, Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE]);

            return new BajajGeneratorMesh(topology);
        }

        static Vector2[] PolylinePoints(Polyline line)
        {
            Vector2[] pts = new Vector2[line.PointCount];
            int i = 0;
            foreach (PolylineIndex idx in new PolylineVertexEnum(line, 0))
                pts[i++] = new Vector2(line[idx]);
            return pts;
        }

        [TestMethod]
        public void Fixture_DoesNotIntersectInXy()
        {
            Assert.IsFalse(Upper381857.Intersects(Lower381868),
                "The live pair must remain a non-overlapping case; if annotations moved, refresh the fixture.");
            Assert.AreEqual(2, Upper381857.PointCount);
            Assert.AreEqual(2, Lower381868.PointCount);
        }

        /// <summary>
        /// Virtual overlap must either translate one partner onto the other or decline cleanly.
        /// Declining must not leave a half-applied offset array.
        /// </summary>
        [TestMethod]
        public void VirtualOverlap_OnNonOverlappingOpenCurves_IsDeterministic()
        {
            bool[,] linked = new bool[2, 2];
            linked[0, 0] = linked[1, 1] = linked[0, 1] = linked[1, 0] = true;

            IShape2D[] shapesA = [Lower381868, Upper381857];
            IShape2D[] shapesB = [new Polyline(PolylinePoints(Lower381868)), new Polyline(PolylinePoints(Upper381857))];
            bool[] isUpper = [false, true];

            Vector2[] offsetsA = SliceTopology.TryTranslateNonOverlappingShapes(shapesA, isUpper, linked);
            Vector2[] offsetsB = SliceTopology.TryTranslateNonOverlappingShapes(shapesB, isUpper, linked);

            if (offsetsA is null)
            {
                Assert.IsNull(offsetsB, "Virtual overlap must decline the same way on repeated calls.");
                Assert.IsFalse(shapesA[0].Intersects(shapesA[1]), "Declined overlap must leave shapes unmoved.");
                return;
            }

            Assert.IsNotNull(offsetsB);
            Assert.AreEqual(offsetsA[0], offsetsB[0]);
            Assert.AreEqual(offsetsA[1], offsetsB[1]);
            Assert.IsTrue(shapesA[0].Intersects(shapesA[1]) || OverlapsInterior(shapesA[0], shapesA[1]),
                "A successful virtual-overlap pass must leave the pair intersecting.");
        }

        static bool OverlapsInterior(IShape2D a, IShape2D b)
        {
            ShapeRelation relation = a.GetRelation(b);
            return relation != ShapeRelation.None && relation != ShapeRelation.Touching;
        }

        /// <summary>
        /// GenerateFaces must not invent zero-length edges. Those crash BajajTest line views
        /// ("Can't create line with two identical points") and leave hole/isolated counts elevated.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_NonOverlappingOpenCurves_HaveNoZeroLengthEdges()
        {
            BajajGeneratorMesh mesh = MeshFromPair(Lower381868, Upper381857);
            BajajMeshGenerator.GenerateFaces(mesh);

            int zeroLength = 0;
            foreach (var kv in mesh.Edges)
            {
                MorphMeshVertex a = mesh.Vertices[kv.Key.A];
                MorphMeshVertex b = mesh.Vertices[kv.Key.B];
                if (a.Position.XY() == b.Position.XY())
                    zeroLength++;
            }

            Assert.AreEqual(0, zeroLength,
                $"Mesh has {zeroLength} zero-length edge(s) after GenerateFaces. Report: {mesh.ManifoldReport}");
        }

        /// <summary>
        /// A linked pair of short open curves should produce faces (a ribbon), not an empty mesh.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_NonOverlappingOpenCurves_ProduceFaces()
        {
            BajajGeneratorMesh mesh = MeshFromPair(Lower381868, Upper381857);
            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsTrue(mesh.Faces.Count > 0,
                $"Expected ribbon faces for linked OPENCURVE pair. Report: {mesh.ManifoldReport}");
        }

        /// <summary>
        /// Isolated edges after tiling are a real defect for a simple linked pair — not the open-end
        /// boundary that a healthy ribbon is allowed to report.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_NonOverlappingOpenCurves_HaveNoIsolatedEdges()
        {
            BajajGeneratorMesh mesh = MeshFromPair(Lower381868, Upper381857);
            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;
            Assert.AreEqual(0, report.IsolatedEdges,
                $"Linked OPENCURVE ribbon left isolated edges. {report}");
            Assert.AreEqual(0, report.NonManifoldEdges,
                $"Linked OPENCURVE ribbon became non-manifold. {report}");
        }

        /// <summary>
        /// Synthetic control: parallel open curves (XY-disjoint, same as typical ribbons) must still
        /// tile cleanly once virtual overlap brings their boxes together.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_ParallelOpenCurves_AreManifoldExceptOpenEnds()
        {
            Polyline lower = new(
            [
                new Vector2(0, 0),
                new Vector2(13.333, 0),
                new Vector2(26.667, 0),
                new Vector2(40, 0),
            ]);
            Polyline upper = new(
            [
                new Vector2(0, 8),
                new Vector2(13.333, 8),
                new Vector2(26.667, 8),
                new Vector2(40, 8),
            ]);
            Assert.IsFalse(lower.Intersects(upper), "Control setup: parallel ribbons start XY-disjoint.");

            BajajGeneratorMesh mesh = MeshFromPair(lower, upper);
            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;
            Assert.IsTrue(mesh.Faces.Count > 0, $"Expected faces. {report}");
            Assert.AreEqual(0, report.NonManifoldEdges, $"Unexpected non-manifold. {report}");
            Assert.AreEqual(0, report.IsolatedEdges, $"Unexpected isolated edges. {report}");
            Assert.AreEqual(0, report.SingleTrianglePolylinePairs, $"Unexpected single-tri pair. {report}");
        }

        /// <summary>
        /// Same geometry as 381857/381868 but with midpoints inserted so each contour has 3 segments.
        /// Isolates whether the live failure is short (2-point) polylines vs the XY gap itself.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_Densified381857Pair_HasNoIsolatedEdges()
        {
            Polyline densifiedLower = Densify(Lower381868, 2);
            Polyline densifiedUpper = Densify(Upper381857, 2);
            BajajGeneratorMesh mesh = MeshFromPair(densifiedLower, densifiedUpper);
            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;
            Assert.IsTrue(mesh.Faces.Count > 0, $"Expected faces. {report}");
            Assert.AreEqual(0, report.IsolatedEdges, $"Densified pair still has isolated edges. {report}");
            Assert.AreEqual(0, report.NonManifoldEdges, $"Densified pair became non-manifold. {report}");
        }

        static Polyline Densify(Polyline line, int midpointsPerSegment)
        {
            List<Vector2> pts = [];
            Vector2[] src = PolylinePoints(line);
            for (int i = 0; i < src.Length - 1; i++)
            {
                pts.Add(src[i]);
                for (int m = 1; m <= midpointsPerSegment; m++)
                {
                    double t = (double)m / (midpointsPerSegment + 1);
                    pts.Add(new Vector2(
                        src[i].X + ((src[i + 1].X - src[i].X) * t),
                        src[i].Y + ((src[i + 1].Y - src[i].Y) * t)));
                }
            }

            pts.Add(src[^1]);
            return new Polyline([.. pts]);
        }
    }
}
