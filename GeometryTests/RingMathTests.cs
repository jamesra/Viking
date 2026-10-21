using System;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class RingMathTests
    {
        private static Vector2[] OpenSquare() => new[] { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2) };

        private static Vector2[] ClosedSquare() => new[] { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2), new Vector2(0, 0) };

        [TestMethod]
        public void ClosedCount_AddsOnePointOnlyWhenRingIsOpen()
        {
            Assert.AreEqual(5, RingMath.ClosedCount(OpenSquare()));
            Assert.AreEqual(5, RingMath.ClosedCount(ClosedSquare()));
        }

        [TestMethod]
        public void ClosedCount_RejectsNullAndEmpty()
        {
            Assert.ThrowsException<ArgumentNullException>(() => RingMath.ClosedCount(null));
            Assert.ThrowsException<InvalidOperationException>(() => RingMath.ClosedCount(Array.Empty<Vector2>()));
        }

        [TestMethod]
        public void ShiftedAt_WrapsPastTheEndToPointZeroAndSubtractsShift()
        {
            Vector2[] ring = OpenSquare();
            Vector2 shift = new(1, 1);

            Assert.AreEqual(new Vector2(1, 1), RingMath.ShiftedAt(ring, 2, shift));
            Assert.AreEqual(new Vector2(-1, -1), RingMath.ShiftedAt(ring, ring.Length, shift));
        }

        [TestMethod]
        public void ShiftedAverage_DropsClosingDuplicateFromOpenAndClosedRings()
        {
            Assert.AreEqual(new Vector2(1, 1), RingMath.ShiftedAverage(ClosedSquare(), 5, Vector2.Zero));
            Assert.AreEqual(new Vector2(1, 1), RingMath.ShiftedAverage(OpenSquare(), 5, Vector2.Zero));
            Assert.AreEqual(new Vector2(0, 0), RingMath.ShiftedAverage(ClosedSquare(), 5, new Vector2(1, 1)));
        }

        [TestMethod]
        public void ShiftedArea_IsPositiveCounterClockwiseAndNegativeClockwise()
        {
            Vector2[] ccw = ClosedSquare();
            Vector2[] cw = new[] { new Vector2(0, 0), new Vector2(0, 2), new Vector2(2, 2), new Vector2(2, 0), new Vector2(0, 0) };

            Assert.AreEqual(4.0, RingMath.ShiftedArea(ccw, 5, Vector2.Zero, Vector2.Zero), 1e-12);
            Assert.AreEqual(-4.0, RingMath.ShiftedArea(cw, 5, Vector2.Zero, Vector2.Zero), 1e-12);
        }

        [TestMethod]
        public void ShiftedArea_IsIndependentOfOriginAndOpenOrClosedForm()
        {
            Assert.AreEqual(4.0, RingMath.ShiftedArea(OpenSquare(), 5, Vector2.Zero, new Vector2(1e6, -1e6)), 1e-6);
            Assert.AreEqual(4.0, ClosedSquare().PolygonArea(), 1e-12);
            Assert.AreEqual(4.0, OpenSquare().PolygonArea(), 1e-12);
        }
    }
}
