using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests.Meshing
{
    /// <summary>
    /// <see cref="MeshExtensions.Triangulate(Polygon, int, TriangulationMesh{IVertex2D{PolygonIndex}}.ProgressUpdate)"/>
    /// removes outside faces with a linear face walk instead of a midpoint test per diagonal. These tests require the
    /// walk to leave exactly the faces and edges the midpoint test leaves, and to tile the polygon.
    /// </summary>
    [TestClass]
    public class TriangulateFaceWalkTests
    {
        private static Vector2[] Closed(IEnumerable<Vector2> points)
        {
            Vector2[] ring = [.. points];
            return [.. ring, ring[0]];
        }

        /// <summary>A simple polygon: every vertex is at a distinct angle around the origin, so it is star-shaped.</summary>
        private static Polygon Star(int vertices, double innerRadius, double outerRadius, int seed, Vector2 center = default)
        {
            Random random = new(seed);
            return new Polygon(Closed(Enumerable.Range(0, vertices).Select(i =>
            {
                double angle = 2 * Math.PI * i / vertices;
                double radius = innerRadius + (random.NextDouble() * (outerRadius - innerRadius));
                return new Vector2(center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle)));
            })));
        }

        private static Vector2[] Circle(Vector2 center, double radius, int vertices) =>
            Closed(Enumerable.Range(0, vertices).Select(i => new Vector2(
                center.X + (radius * Math.Cos(2 * Math.PI * i / vertices)),
                center.Y + (radius * Math.Sin(2 * Math.PI * i / vertices)))));

        private static Polygon CShape() => new(Closed([
            new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 3), new Vector2(3, 3),
            new Vector2(3, 7), new Vector2(10, 7), new Vector2(10, 10), new Vector2(0, 10)]));

        private static Polygon Comb(int teeth)
        {
            List<Vector2> points = [new Vector2(0, 0), new Vector2((teeth * 2) - 1, 0)];
            for (int i = teeth - 1; i >= 0; i--)
            {
                points.Add(new Vector2((i * 2) + 1, 5));
                points.Add(new Vector2(i * 2, 5));
                if (i > 0)
                {
                    points.Add(new Vector2(i * 2, 1));
                    points.Add(new Vector2((i * 2) - 1, 1));
                }
            }

            return new Polygon(Closed(points));
        }

        public static IEnumerable<object[]> Shapes()
        {
            yield return ["convex", Star(12, 10, 10.001, 1)];
            yield return ["star-small", Star(20, 4, 10, 2)];
            yield return ["star-large", Star(300, 5, 10, 3)];
            yield return ["c-shape", CShape()];
            yield return ["comb", Comb(8)];

            Polygon oneHole = Star(60, 8, 10, 4);
            oneHole.AddInteriorRing(Circle(default, 3, 24));
            yield return ["one-hole", oneHole];

            Polygon triangleHole = Star(40, 8, 10, 5);
            triangleHole.AddInteriorRing(Closed([new Vector2(-2, -2), new Vector2(2, -2), new Vector2(0, 2)]));
            yield return ["triangle-hole", triangleHole];

            Polygon twoHoles = Star(80, 9, 10, 6);
            twoHoles.AddInteriorRing(Circle(new Vector2(-4, 0), 2, 16));
            twoHoles.AddInteriorRing(Circle(new Vector2(4, 0), 2, 16));
            yield return ["two-holes", twoHoles];

            Polygon concaveWithHole = CShape();
            concaveWithHole.AddInteriorRing(Circle(new Vector2(6.5, 1.5), 0.8, 10));
            yield return ["c-shape-hole", concaveWithHole];
        }

        private static string FaceKey(IFace face) => string.Join(",", face.iVerts.OrderBy(v => v));

        private static string EdgeKey(IEdgeKey key) => $"{Math.Min(key.A, key.B)}-{Math.Max(key.A, key.B)}";

        private static double TotalFaceArea(TriangulationMesh<IVertex2D<PolygonIndex>> mesh) =>
            mesh.Faces.Sum(f => mesh.ToTriangle(f).Area);

        [DataTestMethod]
        [DynamicData(nameof(Shapes), DynamicDataSourceType.Method)]
        public void FaceWalk_LeavesTheSameFacesAndEdgesAsTheMidpointTest(string name, Polygon polygon)
        {
            TriangulationMesh<IVertex2D<PolygonIndex>> walked = MeshExtensions.TriangulateCore(polygon, 0, null, useFaceWalkCleanup: true);
            TriangulationMesh<IVertex2D<PolygonIndex>> midpoint = MeshExtensions.TriangulateCore(polygon, 0, null, useFaceWalkCleanup: false);

            CollectionAssert.AreEquivalent(midpoint.Faces.Select(FaceKey).ToList(), walked.Faces.Select(FaceKey).ToList(), $"{name}: faces differ");
            CollectionAssert.AreEquivalent(midpoint.Edges.Keys.Select(EdgeKey).ToList(), walked.Edges.Keys.Select(EdgeKey).ToList(), $"{name}: edges differ");
        }

        [DataTestMethod]
        [DynamicData(nameof(Shapes), DynamicDataSourceType.Method)]
        public void FaceWalk_TilesThePolygonExactly(string name, Polygon polygon)
        {
            TriangulationMesh<IVertex2D<PolygonIndex>> mesh = polygon.Triangulate();

            double expectedArea = polygon.ExteriorRing.PolygonArea() - polygon.InteriorRings.Sum(r => Math.Abs(r.PolygonArea()));
            Assert.AreEqual(Math.Abs(expectedArea), TotalFaceArea(mesh), Math.Abs(expectedArea) * 1e-9, $"{name}: face areas do not sum to the polygon area");

            int vertexCount = polygon.TotalUniqueVertices;
            int expectedFaces = vertexCount + (2 * polygon.InteriorRings.Count) - 2;
            Assert.AreEqual(expectedFaces, mesh.Faces.Count, $"{name}: a triangulation of the region with no extra points has V + 2H - 2 faces");

            foreach (IFace face in mesh.Faces)
            {
                Assert.IsTrue(polygon.Translate(-polygon.Centroid).Contains(mesh.Centroid(face)), $"{name}: face {face} lies outside the polygon");
            }
        }
    }
}
