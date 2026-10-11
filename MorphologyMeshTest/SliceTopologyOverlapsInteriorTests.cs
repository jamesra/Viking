using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Pins <see cref="SliceTopology.OverlapsInterior"/>, the virtual-overlap placement test, across the
    /// boundary-contact relations. Tangent pairs must be rejected whether the contact is nestled
    /// (<see cref="ShapeRelation.Touching"/>) or from outside (<c>Touching | Exterior</c>, Geometry.Core since the
    /// mirror of client 21c05bfe); a tangent pair yields near-coincident correspondence verticies that crash
    /// divide-and-conquer Delaunay.
    /// </summary>
    [TestClass]
    public class SliceTopologyOverlapsInteriorTests
    {
        /// <summary>Axis-aligned square at volume-scale coordinates, counter-clockwise.</summary>
        static Polygon Square(double x, double y, double size) => new(
        [
            new Vector2(x, y),
            new Vector2(x + size, y),
            new Vector2(x + size, y + size),
            new Vector2(x, y + size),
            new Vector2(x, y),
        ]);

        const double X = 34000, Y = 34000;

        [TestMethod]
        public void SharedEdgeFromOutsideIsNotInteriorOverlap()
        {
            Polygon a = Square(X, Y, 100);
            Polygon b = Square(X + 100, Y, 100);
            Assert.AreEqual(ShapeRelation.Touching | ShapeRelation.Exterior, a.GetRelation(b));
            Assert.IsFalse(SliceTopology.OverlapsInterior(a, b));
        }

        [TestMethod]
        public void SharedCornerFromOutsideIsNotInteriorOverlap()
        {
            Polygon a = Square(X, Y, 100);
            Polygon b = Square(X + 100, Y + 100, 100);
            Assert.IsFalse(SliceTopology.OverlapsInterior(a, b));
        }

        [TestMethod]
        public void ExternallyTangentCircleIsNotInteriorOverlap()
        {
            Polygon a = Square(X, Y, 100);
            Circle c = new(new Vector2(X + 150, Y + 50), 50);
            Assert.AreEqual(ShapeRelation.Touching | ShapeRelation.Exterior, a.GetRelation(c));
            Assert.IsFalse(SliceTopology.OverlapsInterior(a, c));
        }

        [TestMethod]
        public void NestledAgainstBoundaryIsNotInteriorOverlap()
        {
            Polygon outer = Square(X, Y, 100);
            Polygon inner = Square(X, Y, 50);
            Assert.AreEqual(ShapeRelation.Touching, outer.GetRelation(inner));
            Assert.IsFalse(SliceTopology.OverlapsInterior(outer, inner));
        }

        [TestMethod]
        public void CrossingAndStrictlyNestedShapesOverlapInterior()
        {
            Polygon a = Square(X, Y, 100);
            Assert.IsTrue(SliceTopology.OverlapsInterior(a, Square(X + 50, Y + 50, 100)));
            Assert.IsTrue(SliceTopology.OverlapsInterior(a, Square(X + 25, Y + 25, 50)));
        }

        [TestMethod]
        public void DisjointShapesDoNotOverlapInterior()
        {
            Assert.IsFalse(SliceTopology.OverlapsInterior(Square(X, Y, 100), Square(X + 200, Y, 100)));
        }
    }
}
