using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Covers the LocationLink gate and the polyline fork partition.
    ///
    /// The shared fixture is the "W" a doubled-back process makes: A-z0, B-z10, C-z0, D-z10, E-z0 linked
    /// A-B, B-C, C-D, D-E.  Slice construction expands along Z links until it reaches a fixed point, so all five
    /// annotations land in one slice with A, C, E below and B, D above, and Bajaj is then free to tile pairs the
    /// annotator never joined.
    /// </summary>
    [TestClass]
    public class PolylineForkTests
    {
        //Shape indices inside the W fixture.
        private const int A = 0;
        private const int C = 1;
        private const int E = 2;
        private const int B = 3;
        private const int D = 4;

        private const double LowerZ = 0.0;
        private const double UpperZ = 10.0;

        private static Polyline Line(double y, double startX, double endX, int segments)
        {
            Vector2[] pts = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                pts[i] = new Vector2(startX + ((endX - startX) * t), y);
            }

            return new Polyline(pts);
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
        /// The chain folds back on itself in Y: D sits one unit from A but nineteen from C and twenty from E, even
        /// though D is linked to C and E and not to A.  Nearest-XY assignment therefore hands D's entire contour to A,
        /// which is the pathology the gate exists to stop, and it produces a full run of faces rather than a lone
        /// triangle so the two-face backstop cannot mask it.
        /// </summary>
        private static SliceTopology WTopology()
        {
            Polyline lineA = Line(0, 0, 20, 3);
            Polyline lineB = Line(10, 0, 20, 3);
            Polyline lineC = Line(20, 0, 20, 3);
            Polyline lineD = Line(1, 0, 20, 3);
            Polyline lineE = Line(21, 0, 20, 3);

            //Shape order must match the A/C/E/B/D index constants: lower band first, then upper.
            return new SliceTopology(
                [lineA, lineC, lineE, lineB, lineD],
                [false, false, false, true, true],
                [LowerZ, LowerZ, LowerZ, UpperZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(5, (A, B), (B, C), (C, D), (D, E)));
        }

        /// <summary>
        /// Confirms the fixture still reproduces the defect and that the gate is what removes it.  Without the
        /// ungated half of this comparison, the "no unlinked faces" tests would pass just as happily against a
        /// fixture that never generated them in the first place.
        /// </summary>
        [TestMethod]
        public void WSequence_UnlinkedTilingExistsWithoutTheGate()
        {
            SliceTopology gatedTopology = WTopology();

            BajajGeneratorMesh ungatedMesh = new(new SliceTopology(
                gatedTopology.Shapes,
                gatedTopology.IsUpper,
                gatedTopology.ShapeZ,
                sliceThickness: UpperZ - LowerZ));
            BajajMeshGenerator.GenerateFaces(ungatedMesh);

            BajajGeneratorMesh gatedMesh = new(gatedTopology);
            BajajMeshGenerator.GenerateFaces(gatedMesh);

            Dictionary<string, int> ungated = TiledPairCounts(ungatedMesh);
            Dictionary<string, int> gated = TiledPairCounts(gatedMesh);

            //A+D is the spurious pair: opposite bands, no LocationLink, and adjacent only because the chain folds.
            Assert.IsTrue(ungated.TryGetValue("0+4", out int ungatedCount) && ungatedCount > 1,
                $"Fixture should tile the unlinked A-D pair with more than one face when ungated, so the two-face " +
                $"backstop cannot be credited with the fix.  ungated: {DescribeTiledPairs(ungatedMesh)}");

            Assert.IsFalse(gated.ContainsKey("0+4"),
                $"Gating must remove the unlinked A-D faces.  gated: {DescribeTiledPairs(gatedMesh)}");
        }

        /// <summary>
        /// The regression guard for the gate's scope.  Two shapes in one band are essentially never joined by a
        /// LocationLink, so gating every unlinked pair would reject region closing, medial-axis closing, and cap
        /// faces along with the real defect.  Here two lower polylines meet end to end, giving them a shared vertex
        /// and same-band faces that must survive.
        /// </summary>
        [TestMethod]
        public void Gating_PreservesSameBandFaces()
        {
            Polyline lowerLeft = Line(0, 0, 20, 3);
            Polyline lowerRight = Line(0, 20, 40, 3);
            Polyline upper = Line(1, 0, 40, 6);

            //The two lower shapes are linked to the upper one but never to each other.
            SliceTopology topology = new(
                [lowerLeft, lowerRight, upper],
                [false, false, true],
                [LowerZ, LowerZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(3, (0, 2), (1, 2)));

            Assert.IsTrue(topology.MayTile(0, 1),
                "Two shapes in the same band must be allowed to share faces even with no LocationLink between them.");
            Assert.IsTrue(topology.MayTile(0, 2), "Linked cross-band pairs stay allowed.");

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsTrue(mesh.Faces.Count > 0, $"The slice should still mesh.  {DescribeTiledPairs(mesh)}");
        }

        [TestMethod]
        public void LinkMatrix_IsSymmetricAndReflexive()
        {
            SliceTopology topology = WTopology();

            Assert.IsTrue(topology.HasLinkData, "The W fixture supplies a link matrix.");

            Assert.IsTrue(topology.IsLinked(A, B));
            Assert.IsTrue(topology.IsLinked(B, A));
            Assert.IsTrue(topology.IsLinked(C, C));

            Assert.IsFalse(topology.IsLinked(A, D), "A and D are on opposite bands but were never linked.");
            Assert.IsFalse(topology.IsLinked(E, B), "E and B are on opposite bands but were never linked.");
        }

        /// <summary>
        /// A topology built without link data must behave exactly as it did before the gate existed.
        /// </summary>
        [TestMethod]
        public void NoLinkData_TreatsEveryPairAsLinked()
        {
            SliceTopology topology = new(
                [Line(0, 0, 20, 3), Line(5, 0, 20, 3)],
                [false, true],
                [LowerZ, UpperZ],
                sliceThickness: UpperZ - LowerZ);

            Assert.IsFalse(topology.HasLinkData);
            Assert.IsTrue(topology.IsLinked(0, 1));
        }

        /// <summary>
        /// The whole point of the gate: no face may join two shapes the annotator did not link.
        /// </summary>
        [TestMethod]
        public void WSequence_UnlinkedShapesNeverShareAFace()
        {
            BajajGeneratorMesh mesh = new(WTopology());

            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsTrue(mesh.Faces.Count > 0, "The linked pairs should still tile.");

            AssertNoFaceSpans(mesh, A, D);
            AssertNoFaceSpans(mesh, E, B);
        }

        /// <summary>
        /// Faces are only half the story: an unlinked pair must not even acquire an edge, or region closing and
        /// face generation can still build a surface across it on a later pass.
        /// </summary>
        [TestMethod]
        public void WSequence_UnlinkedShapesNeverShareAnEdge()
        {
            BajajGeneratorMesh mesh = new(WTopology());

            BajajMeshGenerator.GenerateFaces(mesh);

            AssertNoEdgeSpans(mesh, A, D);
            AssertNoEdgeSpans(mesh, E, B);
        }

        /// <summary>
        /// Linked neighbours must still tile once the gate is on, otherwise the gate is just deleting the mesh.
        /// </summary>
        [TestMethod]
        public void WSequence_LinkedPairsStillTile()
        {
            BajajGeneratorMesh mesh = new(WTopology());

            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsTrue(FacesSpanning(mesh, C, D).Any() || FacesSpanning(mesh, C, B).Any(),
                $"C is linked to both B and D and should tile with at least one of them.  Tiled pairs: {DescribeTiledPairs(mesh)}");
        }

        /// <summary>
        /// Two polylines on different sections share a full quad or nothing.  The backstop runs on the polyline
        /// branch of face generation, so a pair reduced to one triangle should end up with none.
        /// </summary>
        [TestMethod]
        public void TwoFaceMinimum_LonePolylineTriangleIsRemoved()
        {
            //Three lower verticies against a single upper segment placed off to one side, so only the nearest
            //corner can tile and the pair is left with a single triangle.
            Polyline lower = Line(0, 0, 30, 3);
            Polyline upper = new([new Vector2(28, 4), new Vector2(34, 4)]);

            SliceTopology topology = new(
                [lower, upper],
                [false, true],
                [LowerZ, UpperZ],
                sliceThickness: UpperZ - LowerZ);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);

            foreach (KeyValuePair<string, int> pair in TiledPairCounts(mesh))
            {
                //Cross-band groups are the ones the backstop governs; "0" or "1" alone are same-shape cap faces.
                if (pair.Key.Contains('+') == false)
                    continue;

                Assert.AreNotEqual(1, pair.Value,
                    $"Cross-band polyline pair {pair.Key} was left with a single triangle.  {DescribeTiledPairs(mesh)}");
            }
        }

        /// <summary>
        /// The backstop must not touch polygon pairs.  TryClosingUntiledRegion legitimately closes a three-vertex
        /// region with one triangle on the polygon branch, so a backstop scoped to shapes rather than polylines would
        /// delete correct output and reopen the hole it had just filled.
        /// </summary>
        [TestMethod]
        public void TwoFaceMinimum_LeavesPolygonPairsAlone()
        {
            Polygon lower = Square(10);
            Polygon upper = Square(8);

            SliceTopology topology = new(
                [lower, upper],
                [false, true],
                [LowerZ, UpperZ],
                sliceThickness: UpperZ - LowerZ);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;

            Assert.AreEqual(0, report.SingleTrianglePolylinePairs,
                $"A polygon slice has no polyline pairs for the backstop to touch.  {report}");
            Assert.AreEqual(0, report.NonManifoldEdges, $"Stacked squares should stay manifold.  {report}");
            Assert.AreEqual(0, report.UnexpectedBoundaryEdges, $"Stacked squares should have no holes.  {report}");
        }

        private static Polygon Square(double halfWidth) =>
            new(
            [
                new Vector2(-halfWidth, -halfWidth),
                new Vector2(halfWidth, -halfWidth),
                new Vector2(halfWidth, halfWidth),
                new Vector2(-halfWidth, halfWidth),
                new Vector2(-halfWidth, -halfWidth),
            ]);

        #region Fork partitioning

        //Fork fixture: one lower polyline spanning x 0..40, forking to two upper partners of length 30 and 10, so the
        //expected split of the trunk's arc length is 3:1.  The partners sit on opposite sides of the trunk in Y and
        //share no vertex, which is what makes this a fork rather than one line split into two annotations; collinear
        //partners meeting at a point instead produce a correspondence seam and no gap.
        private const int Trunk = 0;
        private const int LongPartner = 1;
        private const int ShortPartner = 2;

        private static SliceTopology ForkTopology(int trunkSegments = 8)
        {
            Polyline trunk = Line(0, 0, 40, trunkSegments);
            Polyline longPartner = Line(6, 0, 30, 6);
            Polyline shortPartner = Line(-6, 34, 44, 2);

            return new SliceTopology(
                [trunk, longPartner, shortPartner],
                [false, true, true],
                [LowerZ, UpperZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(3, (Trunk, LongPartner), (Trunk, ShortPartner)),
                buildForkPartition: true);
        }

        /// <summary>
        /// Nearest-XY assignment gives whichever partner happens to be closer as much of the trunk as it wants.
        /// Weighting by partner length instead means a partner covering three quarters of the trunk's span receives
        /// roughly three quarters of its verticies.
        /// </summary>
        [TestMethod]
        public void Fork_SplitsProportionalToPartnerLength()
        {
            SliceTopology topology = ForkTopology();
            PolylineForkPartition partition = topology.ForkPartition;

            Assert.IsNotNull(partition, "A polyline linked to two polyline partners across the slice should fork.");
            Assert.IsTrue(partition.TryGetRange(Trunk, LongPartner, out int longFirst, out int longLast),
                "The long partner should own a vertex range of the trunk.");
            Assert.IsTrue(partition.TryGetRange(Trunk, ShortPartner, out int shortFirst, out int shortLast),
                "The short partner should own a vertex range of the trunk.");

            int longCount = longLast - longFirst + 1;
            int shortCount = shortLast - shortFirst + 1;
            int total = longCount + shortCount;

            Assert.IsTrue(longCount > shortCount,
                $"The 30-unit partner should get more verticies than the 10-unit partner, got {longCount} vs {shortCount}.");

            //Boundaries snap to whole verticies, so the realised share cannot match the weights exactly.
            double longShare = (double)longCount / total;
            Assert.IsTrue(Math.Abs(longShare - 0.75) <= 0.15,
                $"The 3:1 length ratio should give the long partner roughly 75% of the verticies, got {longShare:P0} ({longCount}/{total}).");
        }

        /// <summary>
        /// Ranges must be contiguous, disjoint, and separated by exactly one contour segment.  That untiled segment
        /// is the visible gap that makes a fork read as a fork instead of a crease, and both of its verticies must be
        /// reported so the manifold report can excuse the resulting single-face edges.
        /// </summary>
        [TestMethod]
        public void Fork_LeavesGapAtBoundaryVertex()
        {
            SliceTopology topology = ForkTopology();
            PolylineForkPartition partition = topology.ForkPartition;

            partition.TryGetRange(Trunk, LongPartner, out int longFirst, out int longLast);
            partition.TryGetRange(Trunk, ShortPartner, out int shortFirst, out int shortLast);

            Assert.AreEqual(0, longFirst, "The first range should start at the first vertex.");
            Assert.AreEqual(longLast + 1, shortFirst, "Ranges must be adjacent, leaving exactly one untiled segment.");

            Assert.IsTrue(partition.IsForkBoundaryVertex(Trunk, longLast), "The end of one range borders the gap.");
            Assert.IsTrue(partition.IsForkBoundaryVertex(Trunk, shortFirst), "The start of the next range borders the gap.");

            Assert.IsFalse(partition.IsForkBoundaryVertex(Trunk, longFirst), "The trunk's own endpoint is not a fork boundary.");
            Assert.IsFalse(partition.IsForkBoundaryVertex(Trunk, shortLast), "The trunk's own endpoint is not a fork boundary.");

            //Across the gap the allocation must actually refuse the chord, or the ranges are decoration.
            Assert.IsFalse(partition.AllowsChord(Trunk, longLast, ShortPartner), "A vertex in the long range must not chord to the short partner.");
            Assert.IsFalse(partition.AllowsChord(Trunk, shortFirst, LongPartner), "A vertex in the short range must not chord to the long partner.");
            Assert.IsTrue(partition.AllowsChord(Trunk, longLast, LongPartner), "A vertex must still chord to its own partner.");
        }

        /// <summary>
        /// Every partner range holds at least two verticies, so each partner spans at least one contour segment and
        /// can receive a full quad rather than a lone triangle.
        /// </summary>
        [TestMethod]
        public void Fork_EachPartnerRangeSpansAtLeastOneSegment()
        {
            SliceTopology topology = ForkTopology();
            PolylineForkPartition partition = topology.ForkPartition;

            foreach (int iPartner in new[] { LongPartner, ShortPartner })
            {
                Assert.IsTrue(partition.TryGetRange(Trunk, iPartner, out int first, out int last));
                Assert.IsTrue(last - first + 1 >= 2,
                    $"Partner {iPartner} got {last - first + 1} vertex/verticies, which cannot form a quad.");
            }
        }

        /// <summary>
        /// A trunk too short to give every partner two verticies plus a gap must not be partitioned at all.  Forcing
        /// a split there would hand a partner an empty or single-vertex range, which is worse than leaving the
        /// existing nearest-vertex assignment in place; link gating still applies either way.
        /// </summary>
        [TestMethod]
        public void Fork_TooFewVerticiesDoesNotFork()
        {
            //Two segments gives three verticies; two partners need two each plus a one-segment gap, so five.
            SliceTopology topology = ForkTopology(trunkSegments: 2);

            Assert.IsNull(topology.ForkPartition,
                "A three-vertex trunk cannot be split across two partners and must be left unpartitioned.");
        }

        /// <summary>
        /// A polyline with only one linked partner is not a fork and must be left alone.
        /// </summary>
        [TestMethod]
        public void Fork_SinglePartnerIsNotAFork()
        {
            SliceTopology topology = new(
                [Line(0, 0, 40, 8), Line(5, 0, 40, 8)],
                [false, true],
                [LowerZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(2, (0, 1)),
                buildForkPartition: true);

            Assert.IsNull(topology.ForkPartition);
        }

        /// <summary>
        /// The chords bordering the gap carry one face each.  They must be counted as fork boundary rather than as
        /// holes, otherwise every fork would report itself as a torn surface.
        /// </summary>
        [TestMethod]
        public void ManifoldReport_ForkGapIsNotAHole()
        {
            SliceTopology topology = ForkTopology();
            BajajGeneratorMesh mesh = new(topology);

            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;

            Assert.IsTrue(report.PolylineForkBoundaryEdges > 0,
                $"The fork gap should contribute boundary edges attributed to the fork.  {report}\n" +
                $"single-face non-contour edges: {DescribeSingleFaceNonContourEdges(mesh)}\n" +
                $"boundary verticies: {DescribeForkBoundaries(topology)}");

            Assert.AreEqual(2, report.PolylineForkBoundaryEdges,
                $"One gap has exactly two bordering chords, one per partner.  {report}\n" +
                $"single-face non-contour edges: {DescribeSingleFaceNonContourEdges(mesh)}");

            //An open polyline ribbon always leaves its two ends unclosed, which predates fork support; see
            //PolylineRibbonTests.GenerateFaces_OpenRibbonEndsAreReportedAsBoundary.  What matters here is that the
            //fork gap adds nothing to that count instead of showing up as two more holes.
            Assert.AreEqual(2, report.UnexpectedBoundaryEdges,
                $"The fork gap must not add holes beyond the ribbon's own two open ends.  {report}\n" +
                $"single-face non-contour edges: {DescribeSingleFaceNonContourEdges(mesh)}");
        }

        /// <summary>
        /// Every chord between two polylines must carry a type inside IsValid()'s mask.  FLYING is not, and a chord
        /// typed FLYING is a deletion candidate for RemoveInvalidEdges, so the tiling would survive only for as long
        /// as that cleanup keeps running before chord generation rather than after it.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_PolylineChordsAreValidlyTyped()
        {
            BajajGeneratorMesh mesh = new(ForkTopology());

            BajajMeshGenerator.GenerateFaces(mesh);

            MorphMeshEdge[] chords = [.. mesh.MorphEdges.Where(e => SpansTwoShapes(mesh, e))];
            Assert.IsTrue(chords.Length > 0, "The fork must tile, or there is nothing to check the typing of.");

            EdgeType[] distinct = [.. chords.Select(e => e.Type).Distinct()];
            Assert.AreEqual(0, chords.Count(e => e.Type == EdgeType.FLYING),
                $"Polyline chords typed FLYING fail IsValid().  Types present: {string.Join(", ", distinct)}.");
            Assert.IsTrue(chords.All(e => e.Type.IsValid()),
                $"Every polyline chord must survive IsValid().  Types present: {string.Join(", ", distinct)}.");

            int facesBefore = mesh.Faces.Count;
            mesh.RemoveInvalidEdges();
            Assert.AreEqual(facesBefore, mesh.Faces.Count,
                $"RemoveInvalidEdges deleted fork faces ({facesBefore} -> {mesh.Faces.Count}).");
        }

        private static bool SpansTwoShapes(BajajGeneratorMesh mesh, MorphMeshEdge edge)
        {
            MorphMeshVertex a = mesh[edge.A];
            MorphMeshVertex b = mesh[edge.B];

            if (a.ShapeIndex is null || b.ShapeIndex is null)
                return false;

            return a.ShapeIndex.ShapeIndex != b.ShapeIndex.ShapeIndex;
        }

        /// <summary>Endpoints of every single-face non-contour edge, as shape/vertex pairs, for assertion messages.</summary>
        private static string DescribeSingleFaceNonContourEdges(BajajGeneratorMesh mesh)
        {
            List<string> described = [];
            foreach (KeyValuePair<IEdgeKey, IEdge> kvp in mesh.Edges)
            {
                if (kvp.Value.Faces.Count != 1)
                    continue;

                if (kvp.Value is MorphMeshEdge morphEdge && morphEdge.Type == EdgeType.CONTOUR)
                    continue;

                IShapeIndex a = mesh[kvp.Key.A].ShapeIndex;
                IShapeIndex b = mesh[kvp.Key.B].ShapeIndex;
                string type = kvp.Value is MorphMeshEdge me ? me.Type.ToString() : "?";
                described.Add($"{type} s{a?.ShapeIndex}v{a?.VertexIndex}-s{b?.ShapeIndex}v{b?.VertexIndex}");
            }

            return described.Count == 0 ? "none" : string.Join(", ", described);
        }

        private static string DescribeForkBoundaries(SliceTopology topology)
        {
            PolylineForkPartition partition = topology.ForkPartition;
            if (partition is null)
                return "no fork";

            List<string> described = [];
            foreach (int iShape in partition.ForkedShapes)
            {
                int vertexCount = topology.Shapes[iShape] is Polyline line ? line.PointCount : 0;
                for (int iVert = 0; iVert < vertexCount; iVert++)
                {
                    if (partition.IsForkBoundaryVertex(iShape, iVert))
                        described.Add($"s{iShape}v{iVert}");
                }
            }

            return string.Join(", ", described);
        }

        /// <summary>
        /// Guards the exemption against being too broad.  An edge whose endpoints are both away from the gap must
        /// still count as a hole, so a real tear one segment from the fork is not swallowed by the exemption.
        /// </summary>
        [TestMethod]
        public void ManifoldReport_EdgeAwayFromForkBoundaryIsNotExempt()
        {
            SliceTopology topology = ForkTopology();
            BajajGeneratorMesh mesh = new(topology);

            BajajMeshGenerator.GenerateFaces(mesh);

            PolylineForkPartition partition = topology.ForkPartition;
            int checkedEdges = 0;

            foreach (KeyValuePair<IEdgeKey, IEdge> kvp in mesh.Edges)
            {
                IShapeIndex indexA = mesh[kvp.Key.A].ShapeIndex;
                IShapeIndex indexB = mesh[kvp.Key.B].ShapeIndex;
                if (indexA is null || indexB is null)
                    continue;

                bool touchesBoundary = partition.IsForkBoundaryVertex(indexA.ShapeIndex, indexA.VertexIndex)
                                    || partition.IsForkBoundaryVertex(indexB.ShapeIndex, indexB.VertexIndex);
                if (touchesBoundary)
                    continue;

                checkedEdges++;
                Assert.IsFalse(mesh.IsForkGapBoundaryEdge(kvp.Key),
                    $"Edge ({kvp.Key.A},{kvp.Key.B}) touches no fork boundary vertex and must not be exempt.");
            }

            Assert.IsTrue(checkedEdges > 0, "Expected some edges away from the fork boundary to check.");
        }

        /// <summary>
        /// The pathology the arc-length midpoint rule replaces the weighted rule to fix.  A short partner sitting
        /// between two long ones gets almost none of the trunk under a length-proportional split, and what it does
        /// get is the far tail rather than the stretch of trunk it sits above.  Its range must contain the point it
        /// projects onto, or every one of its chords reaches past its own centre.
        /// </summary>
        [TestMethod]
        public void Fork_ShortMiddlePartnerOwnsItsOwnPosition()
        {
            //Trunk 0..60 with 12 segments.  Partners centred near 10, 30 and 50; the middle one is a third the
            //length of its neighbours, which is what starves it under a weighted split.
            Polyline trunk = Line(0, 0, 60, 12);
            Polyline leftPartner = Line(6, 0, 20, 4);
            Polyline middlePartner = Line(-6, 27, 33, 2);
            Polyline rightPartner = Line(6, 40, 60, 4);

            SliceTopology topology = new(
                [trunk, leftPartner, middlePartner, rightPartner],
                [false, true, true, true],
                [LowerZ, UpperZ, UpperZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(4, (0, 1), (0, 2), (0, 3)),
                buildForkPartition: true);

            PolylineForkPartition partition = topology.ForkPartition;
            Assert.IsNotNull(partition, "A trunk linked to three polyline partners should fork.");

            AssertRangeContainsProjection(partition, Trunk, 2);
        }

        /// <summary>
        /// The rule has to hold for any arity and any spacing, not just the two-partner fixture.  Every partner must
        /// own the point it projects onto, and the ranges must still tile the trunk in order with no overlap.
        /// </summary>
        [TestMethod]
        public void Fork_UnevenlySpacedPartnersEachOwnTheirPosition()
        {
            //Four partners bunched near the start and spread out towards the end, so equal shares would be wrong
            //everywhere along the trunk.
            Polyline trunk = Line(0, 0, 100, 25);
            Polyline[] partners =
            [
                Line(6, 0, 8, 2),
                Line(-6, 12, 22, 2),
                Line(6, 40, 70, 6),
                Line(-6, 85, 100, 3),
            ];

            IShape2D[] shapes = [trunk, .. partners];
            bool[] isUpper = [false, true, true, true, true];
            double[] shapeZ = [LowerZ, UpperZ, UpperZ, UpperZ, UpperZ];

            SliceTopology topology = new(
                shapes,
                isUpper,
                shapeZ,
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(5, (0, 1), (0, 2), (0, 3), (0, 4)),
                buildForkPartition: true);

            PolylineForkPartition partition = topology.ForkPartition;
            Assert.IsNotNull(partition, "A trunk linked to four polyline partners should fork.");

            for (int iPartner = 1; iPartner < shapes.Length; iPartner++)
                AssertRangeContainsProjection(partition, Trunk, iPartner);

            AssertRangesTileTrunkInOrder(partition, Trunk, trunk.PointCount, shapes.Length - 1);
        }

        /// <summary>
        /// The point of placing boundaries by position rather than by partner length is shorter chords, so the mean
        /// distance from a trunk vertex to the partner it was given is the metric that decides whether the change
        /// earned its keep.  It must never increase.
        ///
        /// Equality is allowed because it is the correct answer on a fixture where partner size happens to track
        /// partner spacing, which is precisely the case the weighted rule was sound for: both rules then place the
        /// cut on the same vertex.  A fork where size and spacing are uncorrelated has no such excuse and must be a
        /// strict win, which is what MustImprove pins.
        /// </summary>
        [TestMethod]
        public void Fork_ChordLengthDoesNotRegressAgainstWeightedBoundaries()
        {
            foreach ((string name, SliceTopology topology, int[] partnerShapes, bool mustImprove) in ChordComparisonFixtures())
            {
                PolylineForkPartition partition = topology.ForkPartition;
                Assert.IsNotNull(partition, $"{name}: expected a fork.");

                Polyline trunk = (Polyline)topology.Shapes[Trunk];

                double actual = MeanChordLength(topology, trunk, PartitionAssignment(partition, partnerShapes, trunk.PointCount));
                double weighted = MeanChordLength(topology, trunk, WeightedAssignment(topology, trunk, partnerShapes));

                Console.WriteLine($"{name}: equidistant boundaries {actual:F3}, weighted boundaries {weighted:F3}");

                Assert.IsTrue(actual <= weighted + 1e-9,
                    $"{name}: equidistant boundaries gave a mean chord length of {actual:F3}, worse than the " +
                    $"weighted rule's {weighted:F3}.");

                if (mustImprove)
                    Assert.IsTrue(actual < weighted,
                        $"{name}: this is the fork the midpoint rule exists for and it must beat the weighted rule, " +
                        $"got {actual:F3} against {weighted:F3}.");
            }
        }

        /// <summary>
        /// The decline path is the safety net for every change to boundary placement: a trunk that cannot give each
        /// of N partners two verticies plus a one-segment gap must be left unpartitioned rather than handed a range
        /// somebody computed anyway.  Three partners need eight verticies, so seven must decline.
        /// </summary>
        [TestMethod]
        public void Fork_TooFewVerticiesForThreePartnersDoesNotFork()
        {
            Polyline trunk = Line(0, 0, 60, 6);
            Assert.AreEqual(7, trunk.PointCount, "Fixture must sit one vertex below the (2*3)+(3-1) requirement.");

            SliceTopology topology = new(
                [trunk, Line(6, 0, 20, 4), Line(-6, 25, 35, 2), Line(6, 40, 60, 4)],
                [false, true, true, true],
                [LowerZ, UpperZ, UpperZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(4, (0, 1), (0, 2), (0, 3)),
                buildForkPartition: true);

            Assert.IsNull(topology.ForkPartition,
                "A seven vertex trunk cannot be split across three partners and must be left unpartitioned.");
        }

        private static void AssertRangeContainsProjection(PolylineForkPartition partition, int iShape, int iPartner)
        {
            Assert.IsTrue(partition.TryGetRange(iShape, iPartner, out int first, out int last),
                $"Partner {iPartner} should own a vertex range.");
            Assert.IsTrue(partition.TryGetPartnerArcLength(iShape, iPartner, out double position),
                $"Partner {iPartner} should have a recorded projection.");
            Assert.IsTrue(partition.TryGetVertexArcLength(iShape, first, out double firstLength));
            Assert.IsTrue(partition.TryGetVertexArcLength(iShape, last, out double lastLength));

            //The range is bounded by whole verticies, so a projection can fall up to half a segment outside it and
            //still be nearest that partner.  Allow the half segment on each side rather than a fixed tolerance.
            partition.TryGetVertexArcLength(iShape, Math.Max(first - 1, 0), out double beforeFirst);
            partition.TryGetVertexArcLength(iShape, last + 1, out double afterLast);
            double slackBefore = (firstLength - beforeFirst) / 2;
            double slackAfter = afterLast > lastLength ? (afterLast - lastLength) / 2 : 0;

            Assert.IsTrue(position >= firstLength - slackBefore && position <= lastLength + slackAfter,
                $"Partner {iPartner} projects to arc length {position:F3} but was given verticies " +
                $"[{first}..{last}] spanning [{firstLength:F3}..{lastLength:F3}].");
        }

        private static void AssertRangesTileTrunkInOrder(PolylineForkPartition partition, int iShape, int vertexCount, int partnerCount)
        {
            List<(int First, int Last)> ranges = [];
            for (int iPartner = 1; iPartner <= partnerCount; iPartner++)
            {
                Assert.IsTrue(partition.TryGetRange(iShape, iPartner, out int first, out int last));
                ranges.Add((first, last));
            }

            ranges.Sort((a, b) => a.First.CompareTo(b.First));

            Assert.AreEqual(0, ranges[0].First, "The first range starts at the first vertex.");
            Assert.AreEqual(vertexCount - 1, ranges[^1].Last, "The last range ends at the final vertex.");

            for (int k = 0; k < ranges.Count; k++)
            {
                Assert.IsTrue(ranges[k].Last >= ranges[k].First + 1,
                    $"Range [{ranges[k].First}..{ranges[k].Last}] cannot form a quad.");

                if (k + 1 < ranges.Count)
                    Assert.AreEqual(ranges[k].Last + 1, ranges[k + 1].First,
                        "Ranges must be contiguous and ordered with exactly one untiled segment between them.");
            }
        }

        private static IEnumerable<(string Name, SliceTopology Topology, int[] Partners, bool MustImprove)> ChordComparisonFixtures()
        {
            yield return ("two partner fork", ForkTopology(), [LongPartner, ShortPartner], false);

            //The case the weighted rule cannot express: a long partner sitting over the start of the trunk and a
            //short one over the middle.  Length weighting hands the long partner five sixths of the trunk purely
            //because it is long, so the short partner chords only to the far tail it is nowhere near.
            SliceTopology misCentred = new(
                [Line(0, 0, 100, 25), Line(6, -15, 25, 8), Line(-6, 46, 54, 2)],
                [false, true, true],
                [LowerZ, UpperZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(3, (0, 1), (0, 2)),
                buildForkPartition: true);
            yield return ("long partner centred over the trunk start", misCentred, [1, 2], true);

            SliceTopology middle = new(
                [Line(0, 0, 60, 12), Line(6, 0, 20, 4), Line(-6, 27, 33, 2), Line(6, 40, 60, 4)],
                [false, true, true, true],
                [LowerZ, UpperZ, UpperZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(4, (0, 1), (0, 2), (0, 3)),
                buildForkPartition: true);
            yield return ("short partner between two long", middle, [1, 2, 3], false);

            SliceTopology uneven = new(
                [Line(0, 0, 100, 25), Line(6, 0, 8, 2), Line(-6, 12, 22, 2), Line(6, 40, 70, 6), Line(-6, 85, 100, 3)],
                [false, true, true, true, true],
                [LowerZ, UpperZ, UpperZ, UpperZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(5, (0, 1), (0, 2), (0, 3), (0, 4)),
                buildForkPartition: true);
            yield return ("four unevenly spaced partners", uneven, [1, 2, 3, 4], false);
        }

        /// <summary>Partner shape index owning each trunk vertex, read back from the partition.</summary>
        private static int[] PartitionAssignment(PolylineForkPartition partition, int[] partnerShapes, int vertexCount)
        {
            int[] owner = new int[vertexCount];
            for (int iVert = 0; iVert < vertexCount; iVert++)
            {
                owner[iVert] = -1;
                foreach (int iPartner in partnerShapes)
                {
                    if (partition.AllowsChord(Trunk, iVert, iPartner))
                    {
                        owner[iVert] = iPartner;
                        break;
                    }
                }
            }

            return owner;
        }

        /// <summary>
        /// The boundary placement this change replaced: partners ordered along the trunk, then cut at cumulative
        /// fractions of total partner length.  Reproduced here so the chord-length comparison measures the change
        /// rather than an assumption about it.
        /// </summary>
        private static int[] WeightedAssignment(SliceTopology topology, Polyline trunk, int[] partnerShapes)
        {
            double[] cumulative = CumulativeArcLength(trunk);
            double totalLength = cumulative[^1];

            List<(int Partner, double Position, double Weight)> ordered =
            [
                .. partnerShapes.Select(iPartner =>
                {
                    Polyline partner = (Polyline)topology.Shapes[iPartner];
                    return (iPartner, NearestVertexArcLength(trunk, cumulative, partner), partner.Length);
                })
            ];

            ordered.Sort((a, b) => a.Position.CompareTo(b.Position));

            double weightSum = ordered.Sum(o => o.Weight);
            int[] owner = new int[trunk.PointCount];
            double runningWeight = 0;
            int first = 0;

            for (int k = 0; k < ordered.Count; k++)
            {
                runningWeight += ordered[k].Weight;
                int last = k == ordered.Count - 1
                    ? trunk.PointCount - 1
                    : NearestVertexToArcLength(cumulative, totalLength * (runningWeight / weightSum));

                for (int iVert = first; iVert <= last && iVert < owner.Length; iVert++)
                    owner[iVert] = ordered[k].Partner;

                first = last + 1;
            }

            return owner;
        }

        /// <summary>Mean distance from each trunk vertex to the partner contour it was assigned to.</summary>
        private static double MeanChordLength(SliceTopology topology, Polyline trunk, int[] owner)
        {
            double total = 0;
            int counted = 0;

            for (int iVert = 0; iVert < owner.Length; iVert++)
            {
                if (owner[iVert] < 0)
                    continue;

                Vector2 vertex = trunk.Points[iVert].ToVector2();
                Polyline partner = (Polyline)topology.Shapes[owner[iVert]];

                double nearest = double.MaxValue;
                foreach (IPoint2D point in partner.Points)
                    nearest = Math.Min(nearest, Vector2.Distance(point.ToVector2(), vertex));

                total += nearest;
                counted++;
            }

            return counted == 0 ? 0 : total / counted;
        }

        private static double[] CumulativeArcLength(Polyline line)
        {
            IReadOnlyList<IPoint2D> points = line.Points;
            double[] cumulative = new double[points.Count];

            for (int i = 1; i < points.Count; i++)
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1].ToVector2(), points[i].ToVector2());

            return cumulative;
        }

        /// <summary>The vertex-snapping projection the weighted rule used, kept for the comparison baseline.</summary>
        private static double NearestVertexArcLength(Polyline line, double[] cumulative, Polyline partner)
        {
            Vector2 target = partner.BoundingBox.Center;
            IReadOnlyList<IPoint2D> points = line.Points;

            int nearest = 0;
            double nearestDistance = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                double distance = Vector2.DistanceSquared(points[i].ToVector2(), target);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            return cumulative[nearest];
        }

        private static int NearestVertexToArcLength(double[] cumulative, double targetLength)
        {
            int nearest = 0;
            double nearestDelta = double.MaxValue;
            for (int i = 0; i < cumulative.Length; i++)
            {
                double delta = Math.Abs(cumulative[i] - targetLength);
                if (delta < nearestDelta)
                {
                    nearestDelta = delta;
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// A mesh with no fork has nothing to excuse, so the exemption must never fire and the report must be
        /// unchanged from before fork support existed.
        /// </summary>
        [TestMethod]
        public void ManifoldReport_NoForkMeansNoForkBoundaryEdges()
        {
            SliceTopology topology = new(
                [Line(0, 0, 30, 3), Line(5, 0, 30, 3)],
                [false, true],
                [LowerZ, UpperZ],
                sliceThickness: UpperZ - LowerZ);

            BajajGeneratorMesh mesh = new(topology);
            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsNull(mesh.ForkPartition);
            Assert.AreEqual(0, mesh.ManifoldReport.PolylineForkBoundaryEdges);
        }

        #endregion

        /// <summary>
        /// Two unlinked annotations that coincide in XY must not be stitched by a CORRESPONDING edge.
        /// This also pins the initialization order the gate depends on: PopulateMesh runs from the base
        /// constructor, so BajajGeneratorMesh.Topology has to be assigned by then or the gate silently does nothing.
        /// </summary>
        [TestMethod]
        public void CoincidentUnlinkedShapes_GetNoCorrespondingEdge()
        {
            //Two lower shapes and one upper.  The upper shares its exact XY path with the unlinked lower shape.
            Polyline linkedLower = Line(0, 0, 20, 2);
            Polyline unlinkedLower = Line(50, 50, 70, 2);
            Polyline upper = Line(50, 50, 70, 2);

            SliceTopology topology = new(
                [linkedLower, unlinkedLower, upper],
                [false, false, true],
                [LowerZ, LowerZ, UpperZ],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: UpperZ - LowerZ,
                virtualOverlapOffsets: default,
                shapesAreLinked: LinkMatrix(3, (0, 2)));

            BajajGeneratorMesh mesh = new(topology);

            Assert.IsTrue(mesh.Vertices.Count > 0);

            foreach (MorphMeshEdge edge in mesh.MorphEdges.Where(e => e.Type == EdgeType.CORRESPONDING))
            {
                int shapeA = mesh[edge.A].ShapeIndex.ShapeIndex;
                int shapeB = mesh[edge.B].ShapeIndex.ShapeIndex;
                bool spansUnlinked = (shapeA == 1 && shapeB == 2) || (shapeA == 2 && shapeB == 1);
                Assert.IsFalse(spansUnlinked, "Unlinked coincident shapes must not get a CORRESPONDING edge.");
            }

            Assert.IsFalse(mesh.Vertices.Any(v => v.ShapeIndex.ShapeIndex == 1 && v.Corresponding.HasValue),
                "An unlinked shape's verticies must not record a Corresponding partner.");
        }

        /// <summary>Faces grouped by the sorted set of shapes their verticies come from, e.g. "0+4" -> 3.</summary>
        private static Dictionary<string, int> TiledPairCounts(MorphRenderMesh mesh)
        {
            Dictionary<string, int> counts = [];
            foreach (IFace face in mesh.Faces)
            {
                int[] shapes = [.. face.iVerts.Select(v => mesh[v].ShapeIndex?.ShapeIndex ?? -1).Distinct().OrderBy(s => s)];
                string key = string.Join("+", shapes);
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }

            return counts;
        }

        /// <summary>Readable form of <see cref="TiledPairCounts"/> for assertion messages.</summary>
        private static string DescribeTiledPairs(MorphRenderMesh mesh)
        {
            Dictionary<string, int> counts = TiledPairCounts(mesh);

            return counts.Count == 0
                ? "none"
                : string.Join(", ", counts.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        }

        private static IEnumerable<IFace> FacesSpanning(MorphRenderMesh mesh, int iShapeA, int iShapeB)
        {
            foreach (IFace face in mesh.Faces)
            {
                bool touchesA = false;
                bool touchesB = false;
                foreach (int iVert in face.iVerts)
                {
                    IShapeIndex index = mesh[iVert].ShapeIndex;
                    if (index is null)
                        continue;

                    if (index.ShapeIndex == iShapeA)
                        touchesA = true;
                    if (index.ShapeIndex == iShapeB)
                        touchesB = true;
                }

                if (touchesA && touchesB)
                    yield return face;
            }
        }

        private static void AssertNoFaceSpans(MorphRenderMesh mesh, int iShapeA, int iShapeB)
        {
            int count = FacesSpanning(mesh, iShapeA, iShapeB).Count();
            Assert.AreEqual(0, count, $"{count} face(s) span unlinked shapes {iShapeA} and {iShapeB}.");
        }

        private static void AssertNoEdgeSpans(MorphRenderMesh mesh, int iShapeA, int iShapeB)
        {
            foreach (KeyValuePair<IEdgeKey, IEdge> kvp in mesh.Edges)
            {
                IShapeIndex indexA = mesh[kvp.Key.A].ShapeIndex;
                IShapeIndex indexB = mesh[kvp.Key.B].ShapeIndex;
                if (indexA is null || indexB is null)
                    continue;

                bool spans = (indexA.ShapeIndex == iShapeA && indexB.ShapeIndex == iShapeB)
                          || (indexA.ShapeIndex == iShapeB && indexB.ShapeIndex == iShapeA);

                Assert.IsFalse(spans, $"Edge ({kvp.Key.A},{kvp.Key.B}) spans unlinked shapes {iShapeA} and {iShapeB}.");
            }
        }
    }
}
