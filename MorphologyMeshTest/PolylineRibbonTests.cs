using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;

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
        /// Records what an open polyline ribbon actually reports, because the two ends of an open ribbon carry
        /// single-face chords that count as unexpected boundary edges.  That predates fork support, so fork tests
        /// must not expect a fork to reach zero unexpected boundary edges either.
        /// </summary>
        [TestMethod]
        public void GenerateFaces_OpenRibbonEndsAreReportedAsBoundary()
        {
            Polyline lower = HorizontalLine(0, 0, 30, 3);
            Polyline upper = HorizontalLine(5, 0, 30, 3);
            BajajGeneratorMesh mesh = new([lower, upper], [0.0, 10.0], [false, true]);

            BajajMeshGenerator.GenerateFaces(mesh);

            MeshManifoldReport report = mesh.ManifoldReport;

            Assert.AreEqual(0, report.NonManifoldEdges, $"A simple ribbon should not be non-manifold.  {report}");
            Assert.AreEqual(0, report.PolylineForkBoundaryEdges, $"There is no fork here.  {report}");
            Assert.AreEqual(2, report.UnexpectedBoundaryEdges,
                $"An open ribbon has exactly two unclosed ends.  {report}");
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
