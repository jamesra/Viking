using AnnotationVizLib;
using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    [TestClass]
    public class PolylineRibbonTests
    {
        private static Polyline HorizontalLine(double y, double startX, double endX, int segments)
        {
            Vector2[] pts = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                pts[i] = new Vector2(startX + ((endX - startX) * t), y);
            }

            return new Polyline(pts);
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

        /// <summary>
        /// Polylines on a polygon slice stay correspondence-only; polyline-only slices are tiled.
        /// </summary>
        [TestMethod]
        public void IsTileableForBajaj_DropsPolylinesWhenSliceHasPolygons()
        {
            Assert.IsTrue(SliceGraph.IsTileableForBajaj(Square(10), sliceHasPolygon: true));
            Assert.IsFalse(SliceGraph.IsTileableForBajaj(HorizontalLine(0, 0, 10, 3), sliceHasPolygon: true));
            Assert.IsTrue(SliceGraph.IsTileableForBajaj(HorizontalLine(0, 0, 10, 3), sliceHasPolygon: false));
        }

        /// <summary>
        /// Open polyline endpoints must not close a contour ring when the mesh is populated.
        /// </summary>
        [TestMethod]
        public void PopulateMesh_OpenPolylinesDoNotCloseContour()
        {
            Polyline lower = HorizontalLine(0, 0, 30, 3);
            Polyline upper = HorizontalLine(5, 0, 30, 3);
            BajajGeneratorMesh mesh = new([lower, upper], [0.0, 10.0], [false, true]);

            Assert.IsTrue(mesh.Vertices.Count >= 8, "Each polyline vertex should be in the mesh.");
            int contourEdges = mesh.MorphEdges.Count(e => e.Type == EdgeType.CONTOUR);
            Assert.AreEqual(6, contourEdges, "Each 4-vertex open polyline contributes 3 contour edges.");
        }

        /// <summary>
        /// Two stacked open polylines should tile into a ribbon with consistently wound 2-face edges.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_StackedPolylines_ProduceRibbon()
        {
            Polyline lower = HorizontalLine(0, 0, 30, 3);
            Polyline upper = HorizontalLine(5, 0, 30, 3);
            BajajGeneratorMesh mesh = new([lower, upper], [0.0, 10.0], [false, true]);

            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsTrue(mesh.Faces.Count > 0, "Stacked polylines should produce ribbon faces.");
            AssertAllTwoFaceEdgesOpposite(mesh);
        }

        /// <summary>
        /// The stacked-polyline ribbon must still mesh once link gating is on.  A topology carrying a link matrix
        /// takes the gated path through chord validation, correspondence, and Delaunay face creation, so this is the
        /// check that gating a genuinely linked pair changes nothing.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_StackedPolylines_UnaffectedByLinkGating()
        {
            Polyline lower = HorizontalLine(0, 0, 30, 3);
            Polyline upper = HorizontalLine(5, 0, 30, 3);

            BajajGeneratorMesh ungated = new([lower, upper], [0.0, 10.0], [false, true]);
            BajajMeshGenerator.GenerateFaces(ungated);

            bool[,] linked = new bool[2, 2];
            linked[0, 0] = linked[1, 1] = linked[0, 1] = linked[1, 0] = true;

            SliceTopology gatedTopology = new(
                [HorizontalLine(0, 0, 30, 3), HorizontalLine(5, 0, 30, 3)],
                [false, true],
                [0.0, 10.0],
                shapeIndexToMorphNodeIndex: null,
                sliceThickness: 10.0,
                virtualOverlapOffsets: default,
                shapesAreLinked: linked);

            BajajGeneratorMesh gated = new(gatedTopology);
            BajajMeshGenerator.GenerateFaces(gated);

            Assert.IsTrue(gatedTopology.MayTile(0, 1), "The pair is linked, so tiling must be allowed.");
            Assert.AreEqual(ungated.Faces.Count, gated.Faces.Count,
                $"Gating a linked pair changed the ribbon: ungated {ungated.ManifoldReport} vs gated {gated.ManifoldReport}.");
            Assert.AreEqual(ungated.ManifoldReport.ToString(), gated.ManifoldReport.ToString(),
                "Gating a linked pair changed the manifold report.");
            AssertAllTwoFaceEdgesOpposite(gated);
        }

        /// <summary>
        /// An open polyline ribbon is a sheet: the single-face chord at each end is its legitimate boundary, not a
        /// hole.  Counting those two chords as holes marked every gap-junction slice an invalid surface in
        /// BajajMultiTest, so they are attributed to <see cref="MeshManifoldReport.RibbonBoundaryEdges"/> and the
        /// ribbon validates.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_OpenRibbonEndsAreReportedAsRibbonEnds()
        {
            Polyline lower = HorizontalLine(0, 0, 30, 3);
            Polyline upper = HorizontalLine(5, 0, 30, 3);
            BajajGeneratorMesh mesh = new([lower, upper], [0.0, 10.0], [false, true]);

            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;

            Assert.AreEqual(0, report.NonManifoldEdges, $"A simple ribbon should not be non-manifold.  {report}");
            Assert.AreEqual(0, report.PolylineForkBoundaryEdges, $"There is no fork here.  {report}");
            Assert.AreEqual(2, report.RibbonBoundaryEdges,
                $"An open ribbon has exactly two end chords.  {report}");
            Assert.AreEqual(0, report.UnexpectedBoundaryEdges,
                $"The end chords are the sheet boundary, not holes.  {report}");
            Assert.IsTrue(report.IsValidSliceSurface, $"A clean ribbon must validate.  {report}");
        }

        /// <summary>
        /// RPC1 gap junction 52432, locations 368195 (upper) / 368197 (lower).  The lower line runs on past the
        /// upper line's start, roughly collinear with it, so the Delaunay hull of the pair includes a fan from the
        /// lower line's far endpoint across every segment of the upper line on the side away from the ribbon.  That
        /// second sheet gave each upper segment two faces within the slice (three once the slice above added its
        /// own) and left the fan's 960 nm outer edge as a slit in the assembled surface.  Within a slice a polyline
        /// segment carries one face toward a given neighbour.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_LowerLineOvershootsUpperStart_DoesNotFoldRibbon()
        {
            Vector2 origin = new(48500, 35000);
            Polyline upper = new(
            [
                new Vector2(48481.6, 35190.8) - origin, new Vector2(48532.2, 35104.4) - origin, new Vector2(48593.2, 35003.0) - origin,
                new Vector2(48707.1, 34826.6) - origin, new Vector2(48821.8, 34652.1) - origin,
            ]);
            Polyline lower = new(
            [
                new Vector2(48348.0, 35488.1) - origin, new Vector2(48355.5, 35401.8) - origin, new Vector2(48372.1, 35311.0) - origin,
                new Vector2(48409.0, 35238.5) - origin, new Vector2(48458.5, 35160.8) - origin, new Vector2(48563.9, 34980.9) - origin,
                new Vector2(48672.1, 34797.0) - origin,
            ]);

            BajajGeneratorMesh mesh = new([lower, upper], [82460.0, 82530.0], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;
            Assert.IsTrue(report.IsValidSliceSurface, report.ToString());

            foreach (MorphMeshEdge edge in mesh.MorphEdges.Where(e => e.Type == EdgeType.CONTOUR))
            {
                Assert.AreEqual(1, edge.Faces.Count,
                    $"Contour segment {edge.A}-{edge.B} carries {edge.Faces.Count} faces; a ribbon is one sheet, so a segment has one face in its slice.");
            }

            //Both free ends of each line must sit on the sheet boundary: the fold used to bury the upper line's
            //start inside the surface and string the boundary from the lower line's start to the upper line's end.
            int upperStart = mesh.Vertices.First(v => v.ShapeIndex is PolylineIndex { ShapeIndex: 1, VertexIndex: 0 }).Index;
            Assert.IsTrue(mesh[upperStart].Edges.Any(e => mesh[e].Faces.Count == 1),
                "The upper line's first vertex must lie on the ribbon boundary.");
            Assert.AreEqual(2, report.RibbonBoundaryEdges, $"An open ribbon has two end chords.  {report}");
        }

        /// <summary>
        /// RPC1 gap junction 52432, locations 368197 (upper) / 368198 (lower) after BajajMultiTest's process
        /// smoothing.  The two lines cross in XY, so correspondence inserts a shared vertex and the ribbon is a
        /// twisted sheet: before-crossing tiles with before-crossing, after with after.  Delaunay chords from one
        /// line's "before" to the other's "after" used to be accepted as SURFACE edges; the corresponding-vertex
        /// pass then closed quads through them, the contour segments beside the crossing carried two faces and the
        /// proper span was left with a four-edge hole.  <see cref="PolylineSpanPairing"/> rejects those chords.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_CrossingPolylines_TileSpanBySpan()
        {
            Polyline upper = new(
            [
                new Vector2(-250.5, 410.1), new Vector2(-243.0, 323.8), new Vector2(-226.4, 233.0), new Vector2(-189.6, 160.4),
                new Vector2(-140.0, 82.8), new Vector2(-34.6, -97.1), new Vector2(73.6, -281.1),
            ]);
            Polyline lower = new(
            [
                new Vector2(-279.4, 452.2), new Vector2(-266.7, 364.1), new Vector2(-248.2, 280.9), new Vector2(-231.7, 252.5),
                new Vector2(-210.1, 222.9), new Vector2(-117.6, 69.1), new Vector2(-14.9, -104.2),
            ]);

            //SliceGraph inserts the shared crossing vertex before meshing; do the same here.
            List<IShape2D> shapes = [lower, upper];
            List<Vector2> corresponding = shapes.AddCorrespondingVertices();
            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies(new[] { lower, upper }, corresponding);
            Assert.AreEqual(1, corresponding.Count, "The lines cross once.");

            BajajGeneratorMesh mesh = new([.. shapes], [82390.0, 82460.0], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;
            Assert.IsTrue(report.IsValidSliceSurface, report.ToString());
            Assert.IsTrue(mesh.Vertices.Any(v => v.Corresponding.HasValue), "The lines cross, so a corresponding vertex pair is expected.");

            foreach (MorphMeshEdge edge in mesh.MorphEdges.Where(e => e.Type == EdgeType.CONTOUR))
            {
                Assert.AreEqual(1, edge.Faces.Count,
                    $"Contour segment {edge.A}-{edge.B} carries {edge.Faces.Count} faces; each span of a twisted ribbon is still one sheet.");
            }

            Assert.AreEqual(2, report.RibbonBoundaryEdges, $"An open ribbon has two end chords.  {report}");
        }

        [TestMethod]
        public void ChordStaysInSpan_SingleCrossing_RejectsChordsAcrossTheTwist()
        {
            //Two lines traced the same way that cross at (0,0); the crossing vertex is present on both.
            Polyline a = new([new Vector2(-10, 5), new Vector2(0, 0), new Vector2(10, -5)]);
            Polyline b = new([new Vector2(-10, -5), new Vector2(0, 0), new Vector2(10, 5)]);

            Assert.IsTrue(PolylineSpanPairing.ChordStaysInSpan(a, 0, b, 0), "before-before is one span");
            Assert.IsTrue(PolylineSpanPairing.ChordStaysInSpan(a, 2, b, 2), "after-after is one span");
            Assert.IsFalse(PolylineSpanPairing.ChordStaysInSpan(a, 0, b, 2), "before-after crosses the twist");
            Assert.IsFalse(PolylineSpanPairing.ChordStaysInSpan(a, 2, b, 0), "after-before crosses the twist");
            Assert.IsTrue(PolylineSpanPairing.ChordStaysInSpan(a, 1, b, 2), "the crossing vertex sits on the seam");
            Assert.IsTrue(PolylineSpanPairing.ChordStaysInSpan(a, 0, b, 1), "the crossing vertex sits on the seam");

            //The same lines with b traced backwards: before on a pairs with after on b.
            Polyline bReversed = new([new Vector2(10, 5), new Vector2(0, 0), new Vector2(-10, -5)]);
            Assert.IsTrue(PolylineSpanPairing.ChordStaysInSpan(a, 0, bReversed, 2));
            Assert.IsFalse(PolylineSpanPairing.ChordStaysInSpan(a, 0, bReversed, 0));

            //Lines that never cross have a single span.
            Polyline c = new([new Vector2(-10, 20), new Vector2(0, 20), new Vector2(10, 20)]);
            Assert.IsTrue(PolylineSpanPairing.ChordStaysInSpan(a, 0, c, 2));
        }

        /// <summary>
        /// The ribbon-end exemption must not leak onto polygon meshes or closed rings; there every single-face
        /// non-contour edge is still a hole.
        /// </summary>
        [TestMethod]
        public void IsRibbonBoundaryEdge_RejectsPolygonsAndClosedRings()
        {
            BajajGeneratorMesh squares = new([Square(10), Square(10)], [0.0, 10.0], [false, true]);
            foreach (var edge in squares.Edges.Keys)
                Assert.IsFalse(squares.IsRibbonBoundaryEdge(edge), "Polygon meshes have no ribbon ends.");

            Polyline ringLower = new([new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(0, 0)]);
            Polyline ringUpper = new([new Vector2(1, 1), new Vector2(11, 1), new Vector2(11, 11), new Vector2(1, 11), new Vector2(1, 1)]);
            BajajGeneratorMesh rings = new([ringLower, ringUpper], [0.0, 10.0], [false, true]);
            foreach (var edge in rings.Edges.Keys)
                Assert.IsFalse(rings.IsRibbonBoundaryEdge(edge), "A CLOSEDCURVE ring has no free endpoint.");
        }

        /// <summary>
        /// Typing a chord that touches a polyline from shapes alone is not answerable: there are no vertex indices to
        /// rebuild the chord with, and a midpoint containment test means nothing against a shape with no interior.
        /// The overload used to answer FLYING, which is outside IsValid()'s mask, so it must now refuse instead of
        /// handing later passes a type that contradicts the chord's own validity gate.
        /// </summary>
        [TestMethod]
        public void GetEdgeType_FromShapesAlone_RejectsPolylines()
        {
            Polyline line = HorizontalLine(0, 0, 30, 3);
            Polygon square = Square(10);
            LineSegment chord = new(new Vector2(0, 0), new Vector2(0, 5));

            Assert.ThrowsException<ArgumentException>(() => chord.GetEdgeType(line, HorizontalLine(5, 0, 30, 3)));
            Assert.ThrowsException<ArgumentException>(() => chord.GetEdgeType(square, line));
            Assert.ThrowsException<ArgumentException>(() => chord.GetEdgeType(line, square));

            //Polygon pairs still answer, so the guard has not swallowed the case this overload does handle.  The
            //midpoint sits inside both squares, which is exactly what INTERNAL means.
            Assert.AreEqual(EdgeType.INTERNAL, chord.GetEdgeType(square, Square(10)));
        }

        /// <summary>
        /// Two polylines that share a vertex XY produce a zero-length chord between the neighbours of a corresponding
        /// vertex.  LineSegment refuses to build one, and the exception used to abort the whole slice
        /// (RPC1 368401/368399: "Can't create line with two identical points").  The chord must type as INVALID.
        /// </summary>
        [TestMethod]
        public void GetEdgeType_CoincidentPolylineVerticies_IsInvalidNotThrow()
        {
            Polyline lower = new([new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0), new Vector2(30, 0)]);
            Polyline upper = new([new Vector2(10, 0), new Vector2(20, 0), new Vector2(20, 10), new Vector2(20, 20)]);
            IShape2D[] shapes = [lower, upper];

            PolylineIndex a = new(0, 2, lower.PointCount);
            PolylineIndex b = new(1, 1, upper.PointCount);
            Assert.AreEqual(lower[a], upper[b], "Fixture: the two indices must land on the same XY.");

            Vector2 midpoint = lower[a];
            Assert.AreEqual(EdgeType.INVALID, EdgeTypeExtensions.GetEdgeType(a, b, shapes, midpoint));
            Assert.AreEqual(EdgeType.INVALID, EdgeTypeExtensions.GetContourEdgeTypeWithOrientation(a, b, shapes, midpoint));

            Polyline[] lines = [lower, upper];
            Assert.AreEqual(EdgeType.INVALID, EdgeTypeExtensions.GetEdgeType(a, b, lines, midpoint));
        }

        /// <summary>
        /// End to end: a polyline pair sharing a whole segment must reach a mesh instead of throwing out of
        /// CompleteCorrespondingVertexFaces.  Quality is not asserted; the crash is.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_PolylinesSharingASegment_DoesNotThrow()
        {
            Polyline lower = new([new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0), new Vector2(30, 0)]);
            Polyline upper = new([new Vector2(10, 0), new Vector2(20, 0), new Vector2(20, 10), new Vector2(20, 20)]);

            BajajGeneratorMesh mesh = new([lower, upper], [0.0, 10.0], [false, true]);
            BajajMeshGenerator.GenerateFaces(mesh);

            Assert.IsTrue(mesh.Faces.Count > 0, $"Expected some ribbon faces. {mesh.ManifoldReport}");
        }

        /// <summary>
        /// A polygon plus a crossing polyline must still populate without treating the line as a closed ring.
        /// </summary>
        [TestMethod]
        public void PopulateMesh_PolygonAndCrossingPolyline_DoesNotThrow()
        {
            Polygon square = Square(10);
            Polyline line = HorizontalLine(0, -20, 20, 4);
            BajajGeneratorMesh mesh = new([square, line], [0.0, 10.0], [false, true]);

            Assert.IsTrue(mesh.Vertices.Count > square.ExteriorRing.Length - 1);
            Assert.IsTrue(mesh.MorphEdges.Any(e => e.Type == EdgeType.CONTOUR));
        }

        /// <summary>
        /// Open-end polyline caps loft to a 50%-scaled copy offset half a section in Z, not a same-XY vertical strip.
        /// </summary>
        [TestMethod]
        public void CapMeshEnd_SinglePolyline_TapersTowardCentroid()
        {
            const double ContourZ = 0.0;
            const double Thickness = 10.0;
            Polyline line = HorizontalLine(0, 0, 30, 3);
            Vector2[] contourXy = PolylinePoints(line);
            Vector2 centroid = Average(contourXy);

            SliceTopology topology = new([line], [true], [ContourZ], sliceThickness: Thickness);
            BajajGeneratorMesh mesh = new(topology);

            mesh.CapMeshEnd(true);

            Assert.IsTrue(mesh.Faces.Count > 0, "Polyline cap should add loft faces.");

            MorphMeshVertex[] capVerts = [.. mesh.Vertices.Where(v => v.MedialAxisIndex.HasValue)];
            Assert.AreEqual(contourXy.Length, capVerts.Length, "One cap vertex per contour vertex.");

            double halfThickness = Thickness / 2.0;
            foreach (Vector2 xy in contourXy)
            {
                Vector2 expected = centroid + ((xy - centroid) * 0.5);
                Assert.IsTrue(
                    capVerts.Any(v =>
                        Vector2.Distance(v.Position.XY(), expected) < 1e-6
                        && Math.Abs(v.Position.Z - (ContourZ + halfThickness)) < 1e-6),
                    $"Missing cap vertex at scaled {expected} Z={ContourZ + halfThickness}.");
            }
        }

        /// <summary>
        /// A bent polyline's section-to-cap loft has XY area; a colinear scale-only strip would not.
        /// </summary>
        [TestMethod]
        public void CapMeshEnd_BentPolyline_LoftHasXyArea()
        {
            Vector2[] pts =
            [
                new Vector2(0, 0),
                new Vector2(20, 0),
                new Vector2(20, 20),
                new Vector2(0, 20),
            ];
            Polyline line = new(pts);
            Vector2 centroid = Average(pts);

            SliceTopology topology = new([line], [true], [0.0], sliceThickness: 10.0);
            BajajGeneratorMesh mesh = new(topology);

            mesh.CapMeshEnd(true);

            MorphMeshVertex[] capVerts = [.. mesh.Vertices.Where(v => v.MedialAxisIndex.HasValue)];
            Assert.AreEqual(pts.Length, capVerts.Length);

            foreach (Vector2 xy in pts)
            {
                Vector2 expected = centroid + ((xy - centroid) * 0.5);
                Assert.IsTrue(
                    capVerts.Any(v => Vector2.Distance(v.Position.XY(), expected) < 1e-6),
                    $"Missing 50% inset cap vertex near {expected}.");
            }

            double xyArea = 0;
            foreach (IFace face in mesh.Faces)
            {
                Vector2 a = mesh[face.iVerts[0]].Position.XY();
                Vector2 b = mesh[face.iVerts[1]].Position.XY();
                Vector2 c = mesh[face.iVerts[2]].Position.XY();
                xyArea += Math.Abs(((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X))) * 0.5;
            }

            Assert.IsTrue(xyArea > 1.0, $"Bent polyline cap loft should have XY area, was {xyArea}.");
        }

        /// <summary>
        /// A closed LINESTRING (first==last) must not receive filled same-polyline Delaunay faces.
        /// CLOSEDCURVE interiors are not part of the annotation shape.
        /// </summary>
        [TestMethod]
        public void AddDelaunayEdges_ClosedPolyline_DoesNotAddSameShapeFaces()
        {
            Polyline lower = ClosedSquarePolyline(0, 0, 20);
            Polyline upper = ClosedSquarePolyline(1, 1, 20);
            BajajGeneratorMesh mesh = new([lower, upper], [0.0, 10.0], [false, true]);

            int contourBefore = mesh.MorphEdges.Count(e => e.Type == EdgeType.CONTOUR);
            Assert.IsTrue(contourBefore >= 8, "Closed polylines should contribute closing contour edges.");

            BajajMeshGenerator.AddDelaunayEdges(mesh);

            foreach (IFace face in mesh.Faces)
            {
                HashSet<int> shapeIds = [];
                foreach (int iVert in face.iVerts)
                {
                    IShapeIndex index = mesh[iVert].ShapeIndex;
                    if (index is not null)
                        shapeIds.Add(index.ShapeIndex);
                }

                Assert.IsFalse(
                    shapeIds.Count == 1 && mesh.Shapes[shapeIds.First()] is Polyline,
                    "Delaunay must not fill the interior of a single polyline contour.");
            }
        }

        /// <summary>
        /// Curved open polylines must also reject same-shape Delaunay fill (the 368202/368203 failure mode).
        /// </summary>
        [TestMethod]
        public void AddDelaunayEdges_BentOpenPolyline_DoesNotAddSameShapeFaces()
        {
            Polyline lower = new(
            [
                new Vector2(0, 0),
                new Vector2(20, 0),
                new Vector2(20, 20),
                new Vector2(0, 20),
            ]);
            Polyline upper = new(
            [
                new Vector2(1, 1),
                new Vector2(21, 1),
                new Vector2(21, 21),
                new Vector2(1, 21),
            ]);
            BajajGeneratorMesh mesh = new([lower, upper], [0.0, 10.0], [false, true]);

            BajajMeshGenerator.AddDelaunayEdges(mesh);

            foreach (IFace face in mesh.Faces)
            {
                HashSet<int> shapeIds = [];
                foreach (int iVert in face.iVerts)
                {
                    IShapeIndex index = mesh[iVert].ShapeIndex;
                    if (index is not null)
                        shapeIds.Add(index.ShapeIndex);
                }

                Assert.IsFalse(
                    shapeIds.Count == 1 && mesh.Shapes[shapeIds.First()] is Polyline,
                    "Bent open polylines must not receive same-shape Delaunay fill faces.");
            }
        }

        private static Polyline ClosedSquarePolyline(double offsetX, double offsetY, double size) =>
            new(
            [
                new Vector2(offsetX, offsetY),
                new Vector2(offsetX + size, offsetY),
                new Vector2(offsetX + size, offsetY + size),
                new Vector2(offsetX, offsetY + size),
                new Vector2(offsetX, offsetY),
            ]);

        /// <summary>
        /// OPENCURVE LINESTRINGs that accidentally repeat the first point must be opened in InitializeShapes.
        /// </summary>
        [TestMethod]
        public async Task InitializeShapes_OpenCurveWithClosingDuplicate_IsOpened()
        {
            Vector2[] closedPts =
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
                new Vector2(0, 0),
            ];
            MorphologyGraph graph = new(1, new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(90, "nm")));
            TestLocation loc = PolylineLocation(1, closedPts, LocationType.OPENCURVE, section: 1);
            Assert.IsNotNull(loc.Geometry(), "Test WKT must parse as SqlGeometry.");
            Assert.AreEqual(SupportedGeometryType.POLYLINE, loc.Geometry().GeometryType());

            graph.AddNode(new MorphologyNode(1, loc, graph));

            Dictionary<ulong, IShape2D> shapes = await SliceGraph.InitializeShapes(graph, Vector2.Zero, ContourSimplifyOptions.Disabled);
            Assert.AreEqual(1, shapes.Count, $"Expected one shape, got keys: {string.Join(',', shapes.Keys)}");
            Assert.IsTrue(shapes.TryGetValue(1, out IShape2D shape));
            Assert.IsInstanceOfType(shape, typeof(Polyline));
            Polyline line = (Polyline)shape;
            Assert.AreEqual(4, line.PointCount, "OPENCURVE should drop the duplicate closing vertex.");
            Assert.AreNotEqual(line.Points[0], line.Points[^1]);
        }

        /// <summary>
        /// CLOSEDCURVE keeps first==last so a thin closed ribbon can still form; fill is blocked separately.
        /// </summary>
        [TestMethod]
        public async Task InitializeShapes_ClosedCurveKeepsClosingDuplicate()
        {
            Vector2[] closedPts =
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
                new Vector2(0, 0),
            ];
            MorphologyGraph graph = new(1, new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(90, "nm")));
            graph.AddNode(new MorphologyNode(1, PolylineLocation(1, closedPts, LocationType.CLOSEDCURVE, section: 1), graph));

            Dictionary<ulong, IShape2D> shapes = await SliceGraph.InitializeShapes(graph, Vector2.Zero, ContourSimplifyOptions.Disabled);
            Assert.IsTrue(shapes.TryGetValue(1, out IShape2D shape));
            Polyline line = (Polyline)shape;
            Assert.AreEqual(5, line.PointCount, "CLOSEDCURVE should keep the closing duplicate.");
        }

        static TestLocation PolylineLocation(ulong id, Vector2[] points, LocationType typeCode, int section) =>
            new()
            {
                ID = id,
                ParentID = 1,
                UnscaledZ = section,
                Z = section * 90.0,
                TypeCode = typeCode,
                VolumeGeometryWKT = LineStringWkt(points)
            };

        static string LineStringWkt(IReadOnlyList<Vector2> points) =>
            "LINESTRING (" + string.Join(", ", points.Select(p => string.Format(CultureInfo.InvariantCulture, "{0} {1}", p.X, p.Y))) + ")";

        private static Vector2[] PolylinePoints(Polyline line)
        {
            List<Vector2> pts = [];
            foreach (PolylineIndex idx in new PolylineVertexEnum(line, 0))
                pts.Add(line[idx]);
            return [.. pts];
        }

        private static Vector2 Average(IReadOnlyList<Vector2> pts)
        {
            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < pts.Count; i++)
                sum += pts[i];
            return sum * (1.0 / pts.Count);
        }

        private static void AssertAllTwoFaceEdgesOpposite(Mesh3D<MorphMeshVertex> mesh)
        {
            int shared = 0;
            foreach (KeyValuePair<IEdgeKey, IEdge> kvp in mesh.Edges)
            {
                IFace[] faces = [.. kvp.Value.Faces];
                if (faces.Length != 2)
                    continue;

                shared++;
                bool firstForward = TraversesForward(faces[0].iVerts, kvp.Key.A, kvp.Key.B);
                bool secondForward = TraversesForward(faces[1].iVerts, kvp.Key.A, kvp.Key.B);
                Assert.AreNotEqual(firstForward, secondForward,
                    $"Faces sharing edge ({kvp.Key.A},{kvp.Key.B}) traverse it in the same direction.");
            }

            Assert.IsTrue(shared > 0, "Expected at least one 2-face edge on the ribbon.");
        }

        /// <summary>
        /// Adjacent corresponding vertices must be split on that shared edge. Inserting the midpoint
        /// at Current (instead of Next) splits the previous edge and self-intersects a tight polyline.
        /// </summary>
        [TestMethod]
        public void AddPointsBetweenAdjacentCorrespondingVerticies_InsertsOnSharedEdge()
        {
            Polyline line = new(
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 1),
                new Vector2(0, 1),
                new Vector2(0, 2),
            ]);
            List<Vector2> corresponding = [new Vector2(10, 0), new Vector2(10, 1)];

            SliceTopology.AddPointsBetweenAdjacentCorrespondingVerticies([line], corresponding);

            Assert.AreEqual(6, line.PointCount);
            Assert.AreEqual(new Vector2(10, 0), line.Points[1].ToVector2());
            Assert.AreEqual(new Vector2(10, 0.5), line.Points[2].ToVector2());
            Assert.AreEqual(new Vector2(10, 1), line.Points[3].ToVector2());
        }

        /// <summary>
        /// RC1 cell 476 sections 271/272.  Both shapes are CLOSEDCURVE annotations, which arrive as polylines whose
        /// last point repeats the first, and whose closing segment crosses the rest of the contour.  Correspondence
        /// inserts a vertex where the two contours meet, and the insert used to be blamed for the crossing that the
        /// closing segment had brought with it, so the slice threw and was emitted as an empty topology.
        /// </summary>
        [TestMethod]
        public void AddCorrespondingVertices_OnCrossingClosedCurves_DoesNotThrow()
        {
            Polyline upper = new(new Vector2[]
            {
                new(-18275.311, -1121.444), new(-18272.513, -1121.935), new(-18163.378, -1141.097),
                new(-18058.648, -1167.778), new(-18046.362, -1170.908), new(-17945.400, -1229.864),
                new(-17920.913, -1244.162), new(-17864.087, -1277.345), new(-17956.269, -1242.397),
                new(-17974.437, -1235.510), new(-18069.699, -1199.394), new(-18119.620, -1180.469),
                new(-18264.803, -1125.428), new(-18275.311, -1121.444),
            });

            Polyline lower = new(new Vector2[]
            {
                new(-18260.986, -1152.505), new(-18190.273, -1160.419), new(-18133.112, -1177.182),
                new(-18062.325, -1211.564), new(-18028.617, -1226.130), new(-17964.889, -1241.967),
                new(-17878.412, -1246.284), new(-18260.986, -1152.505),
            });

            List<IShape2D> shapes = [upper, lower];

            List<Vector2> corresponding = shapes.AddCorrespondingVertices();

            Assert.IsTrue(corresponding.Count > 0, "The contours cross, so correspondence must find shared verticies.");
            Assert.IsTrue(upper.PointCount > 14, "The upper contour should have gained verticies where the lower one crosses it.");
            Assert.IsTrue(lower.PointCount > 8, "The lower contour should have gained verticies where the upper one crosses it.");
        }

        private static bool TraversesForward(System.Collections.Immutable.ImmutableArray<int> iVerts, int a, int b)
        {
            for (int i = 0; i < iVerts.Length; i++)
            {
                int x = iVerts[i];
                int y = iVerts[(i + 1) % iVerts.Length];
                if (x == a && y == b)
                    return true;
                if (x == b && y == a)
                    return false;
            }

            return false;
        }
    }
}
