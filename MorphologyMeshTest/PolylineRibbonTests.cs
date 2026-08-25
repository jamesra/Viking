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
