using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    /// <summary>
    /// A grid whose points repeat a control position loses a point to duplicate removal, after which the shared per-grid-size
    /// triangle table indexes past the end of <c>MapPoints</c> (IndexOutOfRangeException building the triangle RTree while
    /// drawing a section). <see cref="GridTransform.Create"/> builds a mesh over the distinct points instead.
    /// </summary>
    [TestClass]
    public class GridTransformDuplicatePointTests
    {
        private static readonly Vector2 Offset = new(100000, 150000);
        private static readonly Rectangle MappedBounds = new(400000, 400200, 50000, 50200);

        /// <summary>3x3 grid at mosaic magnitudes, control = mapped + <see cref="Offset"/>.</summary>
        private static MappingVector2[] Grid3x3()
        {
            MappingVector2[] points = new MappingVector2[9];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Vector2 mapped = new(400000 + (x * 100), 50000 + (y * 100));
                    points[x + (y * 3)] = new MappingVector2(mapped + Offset, mapped);
                }
            }

            return points;
        }

        /// <summary>The top right grid point folded onto the centre point's control position.</summary>
        private static MappingVector2[] FoldedGrid3x3()
        {
            MappingVector2[] points = Grid3x3();
            points[8] = new MappingVector2(points[4].ControlPoint, points[8].MappedPoint);
            return points;
        }

        [TestMethod]
        public void CreateWithDistinctPointsReturnsGrid()
        {
            MappingVector2[] points = Grid3x3();
            TriangulationTransform t = GridTransform.Create(points, MappedBounds, 3, 3, new TransformBasicInfo());

            Assert.IsInstanceOfType(t, typeof(GridTransform));
            Assert.AreEqual(9, t.MapPoints.Length);
        }

        [TestMethod]
        public void ConstructorRejectsRepeatedControlPosition()
        {
            Assert.ThrowsException<ArgumentException>(() => new GridTransform(FoldedGrid3x3(), MappedBounds, 3, 3, new TransformBasicInfo()));
        }

        [TestMethod]
        public void CreateWithRepeatedControlPositionReturnsMeshThatMapsBothWays()
        {
            TriangulationTransform t = GridTransform.Create(FoldedGrid3x3(), MappedBounds, 3, 3, new TransformBasicInfo());

            Assert.IsInstanceOfType(t, typeof(MeshTransform));
            Assert.AreEqual(8, t.MapPoints.Length);

            //The bottom left cell is untouched by the fold
            Vector2 mapped = new(400050, 50025);
            Assert.IsTrue(t.TryInverseTransform(mapped + Offset, out Vector2 inverse));
            Assert.AreEqual(mapped.X, inverse.X, 1e-6);
            Assert.AreEqual(mapped.Y, inverse.Y, 1e-6);

            Assert.IsTrue(t.TryTransform(mapped, out Vector2 forward));
            Assert.AreEqual(mapped.X + Offset.X, forward.X, 1e-6);
            Assert.AreEqual(mapped.Y + Offset.Y, forward.Y, 1e-6);
        }
    }
}
