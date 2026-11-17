using System;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests.Algorithms
{
    /// <summary>
    /// <see cref="PoleOfInaccessibility.MaximumClearCircle"/>: the largest circle inside a polygon that also keeps clear of
    /// an obstacle given as a clearance function.
    /// </summary>
    [TestClass]
    public class MaximumClearCircleTests
    {
        private static Polygon Square(double half) => new(new[]
        {
            new Vector2(-half, -half), new Vector2(half, -half), new Vector2(half, half), new Vector2(-half, half), new Vector2(-half, -half),
        });

        [TestMethod]
        public void NoObstacleIsThePoleOfInaccessibility()
        {
            Circle circle = Square(10).MaximumClearCircle(_ => double.MaxValue);

            Assert.AreEqual(0, circle.Center.X, 0.2);
            Assert.AreEqual(0, circle.Center.Y, 0.2);
            Assert.AreEqual(10, circle.Radius, 0.2);
        }

        [TestMethod]
        public void CentralDiskObstaclePushesTheCircleIntoACorner()
        {
            Polygon square = Square(10);
            Vector2 obstacle = new(0, 0);
            const double obstacleRadius = 6;

            Circle circle = square.MaximumClearCircle(p => Vector2.Distance(p, obstacle) - obstacleRadius);

            Assert.IsTrue(circle.Radius > 0, "there is room in the corners");
            Assert.IsTrue(Vector2.Distance(circle.Center, obstacle) - obstacleRadius >= circle.Radius - 0.2,
                "the circle keeps clear of the obstacle");
            Assert.IsTrue(Math.Abs(circle.Center.X) <= 10 - circle.Radius + 0.2 && Math.Abs(circle.Center.Y) <= 10 - circle.Radius + 0.2,
                "the circle stays inside the square");
            Assert.IsTrue(Math.Abs(circle.Center.X) > 3 && Math.Abs(circle.Center.Y) > 3, "the best spot is towards a corner");
        }

        [TestMethod]
        public void ObstacleCoveringEverythingGivesRadiusZero()
        {
            Circle circle = Square(10).MaximumClearCircle(p => Vector2.Distance(p, Vector2.Zero) - 100);

            Assert.AreEqual(0, circle.Radius);
        }

        [TestMethod]
        public void NullArgumentsThrow()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ((Polygon)null).MaximumClearCircle(_ => 0));
            Assert.ThrowsException<ArgumentNullException>(() => Square(1).MaximumClearCircle(null));
        }
    }
}
