using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="LatticeInterpolation.Sample"/>, the correction field sampling the correction service and its clients
    /// share: bilinear in a full cell, bilinear over the present corners in a partial one, and inverse-distance weights from
    /// the node positions themselves when the cell has no corner.
    /// </summary>
    [TestClass]
    public class LatticeInterpolationTests
    {
        private static TryGetLatticeOffset Lookup(Dictionary<(int, int), Vector2> nodes) =>
            (int gx, int gy, out Vector2 offset) => nodes.TryGetValue((gx, gy), out offset);

        [TestMethod]
        public void AFullCellIsBilinear()
        {
            Dictionary<(int, int), Vector2> nodes = new()
            {
                [(0, 0)] = new Vector2(10, 0),
                [(1, 0)] = new Vector2(20, 0),
                [(0, 1)] = new Vector2(10, 10),
                [(1, 1)] = new Vector2(20, 10),
            };

            (Vector2 offset, bool trusted) = LatticeInterpolation.Sample(new Vector2(500, 500), 1000, 4000, Lookup(nodes));

            Assert.IsTrue(trusted);
            Assert.AreEqual(15, offset.X, 1e-9);
            Assert.AreEqual(5, offset.Y, 1e-9);
        }

        [TestMethod]
        public void APartialCellIsBilinearOverThePresentCorners()
        {
            Dictionary<(int, int), Vector2> nodes = new()
            {
                [(0, 0)] = new Vector2(10, 0),
                [(1, 0)] = new Vector2(20, 0),
            };

            (Vector2 offset, bool trusted) = LatticeInterpolation.Sample(new Vector2(250, 500), 1000, 4000, Lookup(nodes));

            Assert.IsTrue(trusted);
            Assert.AreEqual(12.5, offset.X, 1e-9);
        }

        /// <summary>
        /// A point equally far from two node positions, in a cell with no corner of its own, gets their mean. Placing nodes at
        /// their cell centres instead (the client's old fallback) would weight them unequally.
        /// </summary>
        [TestMethod]
        public void TheFallbackWeighsNodesFromTheirOwnPositions()
        {
            Dictionary<(int, int), Vector2> nodes = new()
            {
                [(0, 0)] = new Vector2(10, 0),
                [(5, 0)] = new Vector2(20, 0),
            };

            (Vector2 offset, bool trusted) = LatticeInterpolation.Sample(new Vector2(25, 0), 10, 100, Lookup(nodes));

            Assert.IsTrue(trusted);
            Assert.AreEqual(15, offset.X, 1e-9);
        }

        [TestMethod]
        public void FarFromEveryNodeThereIsNoSupport()
        {
            Dictionary<(int, int), Vector2> nodes = new() { [(0, 0)] = new Vector2(10, 0) };

            (Vector2 offset, bool trusted) = LatticeInterpolation.Sample(new Vector2(100000, 100000), 1000, 4000, Lookup(nodes));

            Assert.IsFalse(trusted);
            Assert.AreEqual(Vector2.Zero, offset);
        }
    }
}
