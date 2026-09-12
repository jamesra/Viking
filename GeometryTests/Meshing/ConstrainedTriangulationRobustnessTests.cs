using Geometry;
using Geometry.Meshing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace GeometryTests.Meshing
{
    /// <summary>
    /// The ring triangulation used to police its input and output with Debug.Assert, which terminates a debug
    /// process.  A whole-cell run cannot survive that, so bad input is now tolerated or reported as an exception
    /// the region-closing code already handles.
    /// </summary>
    [TestClass]
    public class ConstrainedTriangulationRobustnessTests
    {
        private static Vertex2D[] Square(double half) =>
        [
            new Vertex2D(0, new Vector2(-half, -half)),
            new Vertex2D(1, new Vector2(half, -half)),
            new Vertex2D(2, new Vector2(half, half)),
            new Vertex2D(3, new Vector2(-half, half)),
        ];

        /// <summary>
        /// An interior (Steiner) point outside the ring is dropped instead of asserting; the ring still tiles.
        /// </summary>
        [TestMethod]
        public void InteriorPointOutsideRing_IsDroppedNotAsserted()
        {
            Vertex2D[] ring = Square(10);
            Vertex2D[] interior =
            [
                new Vertex2D(0, new Vector2(0, 0)),
                new Vertex2D(1, new Vector2(50, 50)),
            ];

            TriangulationMesh<IVertex2D<int>> mesh = MeshExtensions.Triangulate(ring, interior);

            Assert.AreEqual(ring.Length + 1, mesh.Vertices.Count, "Only the inside Steiner point should be kept.");
            Assert.IsTrue(mesh.Faces.Count >= 4, "Square with one interior point should tile into at least four faces.");
            Assert.IsFalse(mesh.Vertices.Any(v => v.Position.X > 20 || v.Position.Y > 20), "The exterior point must not be in the mesh.");
        }

        /// <summary>
        /// A well-formed ring with no interior points still triangulates and honours every ring edge.
        /// </summary>
        [TestMethod]
        public void SimpleRing_HonoursEveryConstrainedEdge()
        {
            Vertex2D[] ring = Square(10);

            TriangulationMesh<IVertex2D<int>> mesh = MeshExtensions.Triangulate(ring);

            Assert.AreEqual(2, mesh.Faces.Count);
            for (int i = 0; i < ring.Length; i++)
            {
                EdgeKey key = new(i, (i + 1) % ring.Length);
                Assert.IsTrue(mesh.Contains(key), $"Ring edge {key} missing.");
                Assert.AreEqual(1, mesh[key].Faces.Count, $"Ring edge {key} should border exactly one face.");
            }
        }
    }
}
