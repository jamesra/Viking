using Geometry;
using Geometry.Meshing;
using GeometryTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using WebAnnotation.UI.Commands;
using Vector2 = Geometry.Vector2;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="PolygonCutMeshBuilder.FromIndices"/> feeds the retrace choice overlay, which culls
    /// clockwise triangles; every emitted triangle must be counter-clockwise whatever order the cache holds.
    /// </summary>
    [TestClass]
    public class PolygonCutMeshBuilderTests
    {
        private static double SignedArea2(Vector2 a, Vector2 b, Vector2 c) =>
            ((b.X - a.X) * (c.Y - a.Y)) - ((c.X - a.X) * (b.Y - a.Y));

        private static PolygonCutFill BoxFill() => PolygonCutFill.Create(Primitives.BoxPolygon(10));

        [TestMethod]
        public void ClockwiseInputTrianglesAreEmittedCounterClockwise()
        {
            PolygonCutFill fill = BoxFill();
            int[] tris = [.. fill.TriangleIndices];
            for (int i = 0; i + 2 < tris.Length; i += 3)
                (tris[i], tris[i + 1]) = (tris[i + 1], tris[i]); // flip every triangle's winding

            var mesh = PolygonCutMeshBuilder.FromIndices(fill, tris, Color.Green);

            Assert.AreEqual(tris.Length, mesh.Edges.Length);
            for (int i = 0; i < mesh.Edges.Length; i += 3)
            {
                double area = SignedArea2(fill.Positions[mesh.Edges[i]], fill.Positions[mesh.Edges[i + 1]], fill.Positions[mesh.Edges[i + 2]]);
                Assert.IsTrue(area > 0, "triangle " + (i / 3) + " is not counter-clockwise");
            }
        }

        [TestMethod]
        public void CounterClockwiseInputKeepsItsVertexOrder()
        {
            PolygonCutFill fill = BoxFill();
            int[] tris = [.. fill.TriangleIndices];
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                if (SignedArea2(fill.Positions[tris[i]], fill.Positions[tris[i + 1]], fill.Positions[tris[i + 2]]) < 0)
                    (tris[i], tris[i + 1]) = (tris[i + 1], tris[i]); // make this one CCW
            }

            var mesh = PolygonCutMeshBuilder.FromIndices(fill, tris, Color.Magenta);

            CollectionAssert.AreEqual(tris, mesh.Edges);
        }

        [TestMethod]
        public void VerticesCarryPositionsAndColor()
        {
            PolygonCutFill fill = BoxFill();

            var mesh = PolygonCutMeshBuilder.FromIndices(fill, [.. fill.TriangleIndices], Color.Green);

            Assert.AreEqual(fill.Positions.Count, mesh.Verticies.Length);
            for (int i = 0; i < mesh.Verticies.Length; i++)
            {
                Assert.AreEqual((float)fill.Positions[i].X, mesh.Verticies[i].Position.X);
                Assert.AreEqual((float)fill.Positions[i].Y, mesh.Verticies[i].Position.Y);
                Assert.AreEqual(0f, mesh.Verticies[i].Position.Z);
                Assert.AreEqual(Color.Green, mesh.Verticies[i].Color);
            }
        }

        [TestMethod]
        public void FewerThanThreeIndicesYieldsNoMesh()
        {
            PolygonCutFill fill = BoxFill();

            Assert.IsNull(PolygonCutMeshBuilder.FromIndices(fill, null, Color.Green));
            Assert.IsNull(PolygonCutMeshBuilder.FromIndices(fill, [0, 1], Color.Green));
        }

        [TestMethod]
        public void TrailingPartialTriangleIsIgnored()
        {
            PolygonCutFill fill = BoxFill();
            int[] tris = [.. fill.TriangleIndices.Take(3), 0, 1];

            var mesh = PolygonCutMeshBuilder.FromIndices(fill, tris, Color.Green);

            Assert.AreEqual(3, mesh.Edges.Length);
        }

        [TestMethod]
        public void NullFillThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(() => PolygonCutMeshBuilder.FromIndices(null!, [0, 1, 2], Color.Green));
        }

        [TestMethod]
        public void FromPatchMeshesAPolygonAndReturnsNullForNull()
        {
            Assert.IsNull(PolygonCutMeshBuilder.FromPatch(null!, Color.Green));

            var mesh = PolygonCutMeshBuilder.FromPatch(Primitives.BoxPolygon(5), Color.Green);

            Assert.IsNotNull(mesh);
            Assert.IsTrue(mesh.Edges.Length >= 6 && mesh.Edges.Length % 3 == 0);
        }
    }
}
