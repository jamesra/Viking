using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins the point bookkeeping, bounds caching and point queries that <see cref="ReferencePointBasedTransform"/> gives to
    /// TriangulationTransform and RBFTransform, through a subclass that implements no mapping of its own.
    /// Coordinates use real mosaic magnitudes (hundreds of thousands of pixels).
    /// </summary>
    [TestClass]
    public class ReferencePointBasedTransformTests
    {
        /// <summary>Concrete subclass exposing the protected constructors; the mapping methods are never called.</summary>
        private sealed class PointsOnlyTransform : ReferencePointBasedTransform
        {
            public PointsOnlyTransform(MappingVector2[] points) : base(points, new TransformBasicInfo()) { }
            public PointsOnlyTransform(MappingVector2[] points, bool preserveOrder) : base(points, new TransformBasicInfo(), preserveOrder) { }
            public PointsOnlyTransform(MappingVector2[] points, Rectangle mappedBounds) : base(points, mappedBounds, new TransformBasicInfo()) { }
            public PointsOnlyTransform(MappingVector2[] points, Rectangle mappedBounds, Rectangle controlBounds) : base(points, mappedBounds, controlBounds, new TransformBasicInfo()) { }

            public override Vector2 Transform(in Vector2 Point) => throw new NotImplementedException();
            public override Vector2[] Transform(in Vector2[] Points) => throw new NotImplementedException();
            public override Vector2 InverseTransform(in Vector2 Point) => throw new NotImplementedException();
            public override Vector2[] InverseTransform(in Vector2[] Points) => throw new NotImplementedException();
            public override bool CanTransform(in Vector2 Point) => throw new NotImplementedException();
            public override bool TryTransform(in Vector2 Point, out Vector2 v) => throw new NotImplementedException();
            public override bool[] TryTransform(in Vector2[] Points, out Vector2[] v) => throw new NotImplementedException();
            public override bool CanInverseTransform(in Vector2 Point) => throw new NotImplementedException();
            public override bool TryInverseTransform(in Vector2 Point, out Vector2 v) => throw new NotImplementedException();
            public override bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] v) => throw new NotImplementedException();
        }

        private static MappingVector2 MV(double cx, double cy, double mx, double my) => new(new Vector2(cx, cy), new Vector2(mx, my));

        /// <summary>Four points whose control x order (A,B,C,D) is the reverse of their mapped x order (D,C,B,A).</summary>
        private static MappingVector2[] FourPoints() =>
        [
            MV(100000, 200000, 400003, 50000),   // A
            MV(100001, 200500, 400002, 50500),   // B
            MV(100002, 201000, 400001, 51000),   // C
            MV(100003, 201500, 400000, 51500),   // D
        ];

        private static void AssertRect(double left, double right, double bottom, double top, Rectangle r)
        {
            Assert.AreEqual(left, r.Left, $"Left of {r}");
            Assert.AreEqual(right, r.Right, $"Right of {r}");
            Assert.AreEqual(bottom, r.Bottom, $"Bottom of {r}");
            Assert.AreEqual(top, r.Top, $"Top of {r}");
        }

        [TestMethod]
        public void ConstructorSortsMapPointsByMappedXThenMappedY()
        {
            // The MapPoints doc says "sorted by control point x", but MappingVector2.CompareTo compares mapped points.
            // This pins what Array.Sort actually produces.
            MappingVector2[] input = FourPoints();
            var t = new PointsOnlyTransform([input[1], input[3], input[0], input[2]]);
            CollectionAssert.AreEqual(new[] { input[3], input[2], input[1], input[0] }, t.MapPoints);

            // Equal mapped x falls back to mapped y.
            MappingVector2[] tied = [MV(0, 0, 10, 30), MV(1, 0, 10, 10), MV(2, 0, 10, 20)];
            var u = new PointsOnlyTransform(tied);
            CollectionAssert.AreEqual(new[] { 10.0, 20.0, 30.0 }, u.MapPoints.Select(p => p.MappedPoint.Y).ToArray());
        }

        [TestMethod]
        public void WithoutDuplicatesTheCallersArrayIsSortedInPlaceAndKept()
        {
            MappingVector2[] input = FourPoints();
            var t = new PointsOnlyTransform(input);
            Assert.AreSame(input, t.MapPoints);
            Assert.AreEqual(400000, input[0].MappedPoint.X);
        }

        [TestMethod]
        public void PreserveOrderConstructorKeepsTheGivenOrder()
        {
            MappingVector2[] input = FourPoints();
            MappingVector2[] given = [input[1], input[3], input[0], input[2]];
            var t = new PointsOnlyTransform((MappingVector2[])given.Clone(), preserveOrder: true);
            CollectionAssert.AreEqual(given, t.MapPoints);
        }

        [TestMethod]
        public void BoundsAreComputedLazilyFromMapPoints()
        {
            var t = new PointsOnlyTransform(FourPoints());
            AssertRect(100000, 100003, 200000, 201500, t.ControlBounds);
            AssertRect(400000, 400003, 50000, 51500, t.MappedBounds);
        }

        [TestMethod]
        public void ExplicitBoundsFromConstructorsAreKept()
        {
            Rectangle mapped = new(0, 500000, 0, 60000);
            Rectangle control = new(-10, 200000, -20, 300000);

            var t = new PointsOnlyTransform(FourPoints(), mapped);
            AssertRect(0, 500000, 0, 60000, t.MappedBounds);
            AssertRect(100000, 100003, 200000, 201500, t.ControlBounds);

            var u = new PointsOnlyTransform(FourPoints(), mapped, control);
            AssertRect(0, 500000, 0, 60000, u.MappedBounds);
            AssertRect(-10, 200000, -20, 300000, u.ControlBounds);
        }

        [TestMethod]
        public void DuplicateControlPointIsRemoved()
        {
            MappingVector2[] input = [.. FourPoints(), MV(100000, 200000, 399000, 49000)];
            var t = new PointsOnlyTransform(input);
            Assert.AreEqual(4, t.MapPoints.Length);
            Assert.AreEqual(1, t.MapPoints.Count(p => p.ControlPoint == new Vector2(100000, 200000)));
        }

        [TestMethod]
        public void DuplicateMappedPointIsRemoved()
        {
            MappingVector2[] input = [.. FourPoints(), MV(99000, 199000, 400000, 51500)];
            var t = new PointsOnlyTransform(input);
            Assert.AreEqual(4, t.MapPoints.Length);
            Assert.AreEqual(1, t.MapPoints.Count(p => p.MappedPoint == new Vector2(400000, 51500)));
        }

        [TestMethod]
        public void FewerThanThreePointsAfterDuplicateRemovalThrows()
        {
            MappingVector2[] input = [MV(0, 0, 0, 0), MV(0, 0, 5, 5), MV(10, 10, 20, 20)];
            Assert.ThrowsException<ArgumentException>(() => new PointsOnlyTransform(input));

            // Three distinct points with one duplicate removed still leave three.
            MappingVector2[] enough = [MV(0, 0, 0, 0), MV(0, 0, 5, 5), MV(10, 10, 20, 20), MV(30, 0, 40, 0)];
            Assert.AreEqual(3, new PointsOnlyTransform(enough).MapPoints.Length);
        }

        [TestMethod]
        public void CalculateBoundsOfNullOrEmptyIsEmptyRectangle()
        {
            AssertRect(0, 0, 0, 0, ReferencePointBasedTransform.CalculateControlBounds(null));
            AssertRect(0, 0, 0, 0, ReferencePointBasedTransform.CalculateControlBounds([]));
            AssertRect(0, 0, 0, 0, ReferencePointBasedTransform.CalculateMappedBounds(null));
            AssertRect(0, 0, 0, 0, ReferencePointBasedTransform.CalculateMappedBounds([]));
        }

        [TestMethod]
        public void CalculateBoundsIsTheUnionOfEachTransformsBounds()
        {
            var a = new PointsOnlyTransform(FourPoints());
            var b = new PointsOnlyTransform([MV(90000, 205000, 410000, 40000), MV(95000, 210000, 420000, 45000), MV(99000, 202000, 415000, 49000)]);
            ITransformControlPoints[] both = [a, b];

            AssertRect(90000, 100003, 200000, 210000, ReferencePointBasedTransform.CalculateControlBounds(both));
            AssertRect(400000, 420000, 40000, 51500, ReferencePointBasedTransform.CalculateMappedBounds(both));

            // A single transform gives its own bounds.
            AssertRect(100000, 100003, 200000, 201500, ReferencePointBasedTransform.CalculateControlBounds([a]));
            AssertRect(400000, 400003, 50000, 51500, ReferencePointBasedTransform.CalculateMappedBounds([a]));
        }

        [TestMethod]
        public void IntersectingRectanglesReturnOnlyPointsInsideInTheirOwnSpace()
        {
            MappingVector2[] p = FourPoints();
            var t = new PointsOnlyTransform(FourPoints());

            // Control space: A and B only.
            var control = t.IntersectingControlRectangle(new Rectangle(99999.5, 100001.5, 199999, 200600));
            CollectionAssert.AreEquivalent(new[] { p[0], p[1] }, control);

            // The same rectangle in mapped space holds nothing.
            Assert.AreEqual(0, t.IntersectingMappedRectangle(new Rectangle(99999.5, 100001.5, 199999, 200600)).Count);

            // Mapped space: C and D only.
            var mapped = t.IntersectingMappedRectangle(new Rectangle(399999.5, 400001.5, 50900, 51600));
            CollectionAssert.AreEquivalent(new[] { p[2], p[3] }, mapped);
        }

        [TestMethod]
        public void TranslateAfterBoundsWereReadShiftsControlPointsAndBoundsOnce()
        {
            var t = new PointsOnlyTransform(FourPoints());
            _ = t.ControlBounds; // fill the cache first; see the Translate proposal for the uncached case.
            Vector2 offset = new(-100000, -200000);

            t.Translate(offset);

            MappingVector2[] expected = [.. FourPoints().Select(m => new MappingVector2(m.ControlPoint + offset, m.MappedPoint)).OrderBy(m => m.MappedPoint.X)];
            CollectionAssert.AreEqual(expected, t.MapPoints);
            AssertRect(0, 3, 0, 1500, t.ControlBounds);
            AssertRect(400000, 400003, 50000, 51500, t.MappedBounds);
        }
    }
}
