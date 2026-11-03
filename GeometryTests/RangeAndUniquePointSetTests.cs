using System;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GeoRange = Geometry.Range;

namespace GeometryTests
{
    [TestClass]
    public class RangeTests
    {
        [TestMethod]
        public void ConstructorRejectsMinAboveMax()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new GeoRange(2, 1));
        }

        [TestMethod]
        public void ContainsIsClosedInterval()
        {
            GeoRange r = new(1, 3);

            Assert.IsTrue(r.Contains(1));
            Assert.IsTrue(r.Contains(3));
            Assert.IsFalse(r.Contains(0.999));
            Assert.IsFalse(r.Contains(3.001));
        }

        [TestMethod]
        public void NormalizeMapsMinMaxToZeroOne_AndClipLimitsOutsideValues()
        {
            GeoRange r = new(10, 20);

            Assert.AreEqual(0.5, r.Normalize(15), Tolerance.Epsilon);
            Assert.AreEqual(2.0, r.Normalize(30), Tolerance.Epsilon);
            Assert.AreEqual(1.0, r.Normalize(30, clip: true), Tolerance.Epsilon);
            Assert.AreEqual(0.0, r.Normalize(0, clip: true), Tolerance.Epsilon);
        }

        [TestMethod]
        public void NormalizeOfZeroSpanIsZero()
        {
            Assert.AreEqual(0.0, new GeoRange(5, 5).Normalize(7));
        }

        [TestMethod]
        public void InterpolateInvertsNormalize()
        {
            GeoRange r = new(-4, 6);

            Assert.AreEqual(1.0, r.Interpolate(0.5), Tolerance.Epsilon);
            Assert.AreEqual(0.25, r.Normalize(r.Interpolate(0.25)), Tolerance.Epsilon);
        }

        [TestMethod]
        public void ClipClampsToBounds()
        {
            GeoRange r = new(1, 2);

            Assert.AreEqual(1.0, r.Clip(-5));
            Assert.AreEqual(2.0, r.Clip(9));
            Assert.AreEqual(1.5, r.Clip(1.5));
        }
    }

    [TestClass]
    public class UniquePointSetTests
    {
        [TestMethod]
        public void TryAddRejectsDuplicate_AddThrows()
        {
            UniquePointSet set = new();

            Assert.IsTrue(set.TryAdd(new Vector2(1, 1)));
            Assert.IsFalse(set.TryAdd(new Vector2(1, 1)));
            Assert.ThrowsExactly<ArgumentException>(() => set.Add(new Vector2(1, 1)));
        }

        [TestMethod]
        public void TryFindNearestReturnsClosestPoint_FalseWhenEmpty()
        {
            UniquePointSet set = new();
            Assert.IsFalse(set.TryFindNearest(new Vector2(0, 0), out _, out double emptyDistance));
            Assert.IsTrue(double.IsPositiveInfinity(emptyDistance));

            set.Add(new Vector2(10, 0));
            set.Add(new Vector2(2, 0));

            Assert.IsTrue(set.TryFindNearest(new Vector2(0, 0), out Vector2 nearest, out double distance));
            Assert.AreEqual(new Vector2(2, 0), nearest);
            Assert.AreEqual(2.0, distance, Tolerance.Epsilon);
        }

        [TestMethod]
        public void TryRemoveDropsPointOnce()
        {
            UniquePointSet set = new();
            set.Add(new Vector2(3, 3));

            Assert.IsTrue(set.TryRemove(new Vector2(3, 3), out Vector2 removed));
            Assert.AreEqual(new Vector2(3, 3), removed);
            Assert.IsFalse(set.TryRemove(new Vector2(3, 3), out _));
        }

        [TestMethod]
        public void PointMapKeepsValueOfNearestPoint_AndRejectsDuplicates()
        {
            UniquePointMap<string> map = new();
            map.Add(new Vector2(0, 0), "origin");
            map.Add(new Vector2(5, 5), "far");

            Assert.IsTrue(map.Contains(new Vector2(5, 5)));
            Assert.ThrowsExactly<ArgumentException>(() => map.Add(new Vector2(0, 0), "again"));
            Assert.IsTrue(map.TryFindNearest(new Vector2(4, 4), out Vector2 nearest, out string value, out _));
            Assert.AreEqual(new Vector2(5, 5), nearest);
            Assert.AreEqual("far", value);
        }
    }
}
