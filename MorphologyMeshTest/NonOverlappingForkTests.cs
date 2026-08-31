using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMeshTest
{
    /// <summary>
    /// One polygon linked to two polygons that overlap neither it nor each other.  Cell 476 in RC1 does this near the
    /// bottom of the cell.  Every candidate chord midpoint falls outside both contours, so the chords classify FLYING,
    /// RemoveInvalidEdges discards them, and the wall between the polygons degrades to an untiled region or a hole.
    /// Virtual overlap has to recognise the fork and move both partners far enough to be tiled.
    /// </summary>
    [TestClass]
    public class NonOverlappingForkTests
    {
        private const double LowerZ = 0.0;
        private const double UpperZ = 10.0;

        private const int Trunk = 0;
        private const int PartnerA = 1;
        private const int PartnerB = 2;

        /// <summary>
        /// A circle sampled at <paramref name="nPoints"/> verticies.  Real contours are sampled finely enough for the
        /// medial axis and the OTV tables to have something to work with; a four vertex square is not.
        /// </summary>
        private static Polygon Circle(double radius, Vector2 center, int nPoints = 16)
        {
            Vector2[] ring = new Vector2[nPoints + 1];
            for (int i = 0; i < nPoints; i++)
            {
                double theta = 2.0 * System.Math.PI * i / nPoints;
                ring[i] = new Vector2(center.X + (radius * System.Math.Cos(theta)), center.Y + (radius * System.Math.Sin(theta)));
            }

            ring[nPoints] = ring[0];
            return new Polygon(ring);
        }

        private static bool[,] LinkMatrix(int count, params (int A, int B)[] links)
        {
            bool[,] linked = new bool[count, count];
            for (int i = 0; i < count; i++)
                linked[i, i] = true;

            foreach ((int a, int b) in links)
            {
                linked[a, b] = true;
                linked[b, a] = true;
            }

            return linked;
        }

        /// <summary>
        /// The trunk sits at the origin with a partner up and to the right and another down and to the right, so the
        /// partners approach the trunk from different directions and cannot land on each other.
        /// </summary>
        private static SliceTopology ForkTopology()
        {
            Polygon trunk = Circle(20, new Vector2(0, 0));
            Polygon partnerA = Circle(6, new Vector2(60, 40));
            Polygon partnerB = Circle(6, new Vector2(60, -40));

            Assert.IsFalse(trunk.Intersects(partnerA), "Test setup requires the partners to be disjoint from the trunk.");
            Assert.IsFalse(trunk.Intersects(partnerB), "Test setup requires the partners to be disjoint from the trunk.");
            Assert.IsFalse(partnerA.Intersects(partnerB), "Test setup requires the partners to be disjoint from each other.");

            return BuildTopology(trunk, partnerA, partnerB);
        }

        /// <summary>
        /// The same disjoint fork with virtual overlap suppressed: the pre-fix behavior, and the yardstick the
        /// translated mesh has to beat.
        /// </summary>
        private static SliceTopology UntranslatedForkTopology()
        {
            Polygon trunk = Circle(20, new Vector2(0, 0));
            Polygon partnerA = Circle(6, new Vector2(60, 40));
            Polygon partnerB = Circle(6, new Vector2(60, -40));

            return BuildTopology(trunk, partnerA, partnerB, translate: false);
        }

        /// <summary>
        /// Mirrors SliceGraph.GetSliceTopology: virtual overlap runs first, then correspondence, because corresponding
        /// verticies have to be computed in the frame Bajaj will tile in.
        /// </summary>
        private static SliceTopology BuildTopology(Polygon trunk, Polygon partnerA, Polygon partnerB, bool translate = true)
        {
            IShape2D[] shapes = [trunk, partnerA, partnerB];
            bool[] isUpper = [false, true, true];
            bool[,] links = LinkMatrix(3, (Trunk, PartnerA), (Trunk, PartnerB));

            //An all-zero array is not null, so it suppresses the translation the constructor would otherwise compute.
            Vector2[] offsets = translate
                ? SliceTopology.TryTranslateNonOverlappingShapes(shapes, isUpper, links)
                : new Vector2[shapes.Length];

            List<IShape2D> shapeList = [.. shapes];
            var corresponding = shapeList.AddCorrespondingVertices();
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies([.. shapeList.OfType<Polygon>()], corresponding);

            return new SliceTopology(
                shapeList,
                isUpper,
                [LowerZ, UpperZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: offsets,
                shapesAreLinked: links);
        }

        [TestMethod]
        public void Fork_MovesEachPartnerOntoTheTrunkWithoutStackingThem()
        {
            SliceTopology topology = ForkTopology();

            Assert.IsTrue(topology.HasVirtualOverlapTranslation, "The fork's disjoint partners must be translated.");
            Assert.AreEqual(Vector2.Zero, topology.GetVirtualOverlapOffset(Trunk), "The forking shape is the fixed frame.");
            Assert.AreNotEqual(Vector2.Zero, topology.GetVirtualOverlapOffset(PartnerA));
            Assert.AreNotEqual(Vector2.Zero, topology.GetVirtualOverlapOffset(PartnerB));

            Assert.IsTrue(topology.Shapes[PartnerA].Intersects(topology.Shapes[Trunk]), "Partner A must overlap the trunk after translation.");
            Assert.IsTrue(topology.Shapes[PartnerB].Intersects(topology.Shapes[Trunk]), "Partner B must overlap the trunk after translation.");
            Assert.IsFalse(topology.Shapes[PartnerA].Intersects(topology.Shapes[PartnerB]),
                "Pulling both partners onto the trunk centroid would stack them and produce crossing chords.");
        }

        [TestMethod]
        public void Fork_TilesBothPartnersAndLeavesNoHoles()
        {
            BajajGeneratorMesh mesh = new(ForkTopology());

            BajajMeshGenerator.GenerateFaces(mesh);

            HashSet<string> tiledPairs = [.. mesh.MorphFaces.Select(f => CrossBandPair(mesh, f)).Where(p => p is not null)];
            Assert.IsTrue(tiledPairs.Contains($"{Trunk}+{PartnerA}"), $"The trunk must tile to partner A.  Tiled pairs: {string.Join(", ", tiledPairs)}");
            Assert.IsTrue(tiledPairs.Contains($"{Trunk}+{PartnerB}"), $"The trunk must tile to partner B.  Tiled pairs: {string.Join(", ", tiledPairs)}");

            BajajGeneratorMesh untranslated = new(UntranslatedForkTopology());
            BajajMeshGenerator.GenerateFaces(untranslated);

            Assert.AreEqual(0, untranslated.MorphFaces.Count(f => CrossBandPair(untranslated, f) is not null),
                "Fixture check: the untranslated fork must not tile across the slice, or the translation is not what fixed it.");

            MeshManifoldReport report = MeshManifoldValidator.Validate(mesh);
            MeshManifoldReport untranslatedReport = MeshManifoldValidator.Validate(untranslated);
            Assert.AreEqual(0, report.UnexpectedBoundaryEdges,
                $"The fork left holes away from the contour seams.\n  translated: {report}\n  untranslated: {untranslatedReport}\n  untiled regions: {CountUntiledRegions(mesh)}");
            Assert.AreEqual(0, report.NonManifoldEdges, $"Edges shared by three or more faces.  {report}");
            Assert.AreEqual(0, report.InconsistentManifoldEdges, $"Faces disagree across a shared edge.  {report}");
        }

        /// <summary>
        /// Reported only in failure messages.  Region type is derived from edge classification, which the closing
        /// passes do not rewrite, so a closed region still counts here and this is a diagnostic rather than a verdict.
        /// The verdict on untiled regions is the hole count.
        /// </summary>
        private static int CountUntiledRegions(BajajGeneratorMesh mesh)
        {
            mesh.IdentifyRegionsViaFaces();
            return mesh.Regions.Count(r => r.Type == RegionType.UNTILED);
        }

        /// <summary>
        /// A fork whose free partner has to pass its already-overlapping sibling to reach the trunk.  The window that
        /// reaches the trunk while clearing the sibling is narrower than the depth the placement asks for, so the
        /// partner has to be re-placed shallower rather than the whole slice being abandoned.  Abandoning it was the
        /// single largest cause of untiled non-overlapping contours on cell 476.
        /// </summary>
        [TestMethod]
        public void Fork_ShortensTheMoveRatherThanLandingOnASibling()
        {
            //The sibling straddles the trunk boundary just off the line the free partner travels along, so a shallow
            //placement reaches the trunk cleanly and only a deeper one collides.
            Polygon trunk = Circle(20, new Vector2(0, 0));
            Polygon sibling = Circle(10, new Vector2(12.42, 12));
            Polygon free = Circle(6, new Vector2(58, 0));

            Assert.IsTrue(sibling.Intersects(trunk), "Fixture: the sibling already overlaps the trunk and never moves.");
            Assert.IsFalse(free.Intersects(trunk), "Fixture: the free partner is the one with a gap to close.");
            Assert.IsFalse(free.Intersects(sibling), "Fixture: the two partners start apart.");

            IShape2D[] shapes = [trunk, sibling, free];
            Vector2[] offsets = SliceTopology.TryTranslateNonOverlappingShapes(
                shapes, [false, true, true], LinkMatrix(3, (Trunk, PartnerA), (Trunk, PartnerB)));

            Assert.IsNotNull(offsets, "The slice must not be abandoned just because the direct move overshoots.");
            Assert.AreNotEqual(Vector2.Zero, offsets[PartnerB], "The free partner has to move.");
            Assert.AreEqual(Vector2.Zero, offsets[PartnerA], "The sibling already overlaps the trunk, so it stays put.");

            Assert.IsTrue(shapes[PartnerB].Intersects(shapes[Trunk]), "The shortened move must still reach the trunk.");
            Assert.IsFalse(shapes[PartnerB].Intersects(shapes[PartnerA]),
                "The shortened move must not create a same-band overlap the annotator never drew.");
        }

        [TestMethod]
        public void Fork_RestoresPartnerVerticiesToTheirAnnotatedPositions()
        {
            SliceTopology topology = ForkTopology();
            Vector2 offsetA = topology.GetVirtualOverlapOffset(PartnerA);
            Vector2 offsetB = topology.GetVirtualOverlapOffset(PartnerB);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);

            AssertContourVerticiesAreNear(mesh, PartnerA, new Vector2(60, 40), 6, "Partner A");
            AssertContourVerticiesAreNear(mesh, PartnerB, new Vector2(60, -40), 6, "Partner B");
            AssertContourVerticiesAreNear(mesh, Trunk, new Vector2(0, 0), 20, "The trunk, which never moved,");

            //The partners approach from opposite sides, so equal offsets would mean the fork was treated as one move.
            Assert.AreNotEqual(offsetA, offsetB, "Each partner is translated by its own amount.");
        }

        /// <summary>
        /// Every contour vertex of a restored shape has to sit on the circle the annotator drew.  An average would
        /// hide a restore that moved the contour and let correspondence verticies bunch up to compensate.
        /// </summary>
        private static void AssertContourVerticiesAreNear(BajajGeneratorMesh mesh, int iShape, Vector2 center, double radius, string description)
        {
            Vector3[] contour = [.. mesh.MorphVerticies
                .Where(v => v.ShapeIndex is not null && v.ShapeIndex.ShapeIndex == iShape)
                .Select(v => v.Position)];

            Assert.IsTrue(contour.Length > 0, $"{description} should contribute contour verticies to the mesh.");

            foreach (Vector3 p in contour)
            {
                double distance = new Vector2(p.X - center.X, p.Y - center.Y).Magnitude;
                Assert.IsTrue(distance <= radius + 1.0,
                    $"{description} should return to its annotated XY: vertex {p} is {distance:F1} from {center}, outside radius {radius}.");
            }
        }

        /// <summary>
        /// The pair of shapes a face tiles across the slice, or null when every vertex belongs to one band.
        /// </summary>
        private static string CrossBandPair(BajajGeneratorMesh mesh, MorphMeshFace face)
        {
            SortedSet<int> shapes = [];
            foreach (int iVert in face.iVerts)
            {
                MorphMeshVertex v = mesh[iVert];
                if (v.ShapeIndex is null)
                    return null;

                shapes.Add(v.ShapeIndex.ShapeIndex);
            }

            if (shapes.Count < 2 || shapes.Select(i => mesh.Topology.IsUpper[i]).Distinct().Count() < 2)
                return null;

            return string.Join("+", shapes);
        }
    }
}
