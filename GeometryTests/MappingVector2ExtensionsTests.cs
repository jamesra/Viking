using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="StackExtensions{T}.Peek"/>, <see cref="MappingVector2Extensions"/> (ControlBounds / MappedBounds, which size
    /// tile and section mappings) and <see cref="MappingVector2SetExtensions.SetEquals"/> (order-insensitive comparison of mapped point sets).
    /// </summary>
    [TestClass]
    public class MappingVector2ExtensionsTests
    {
        [TestMethod]
        public void Peek_ReturnsTopFirstWithoutPopping()
        {
            Stack<int> stack = new();
            stack.Push(1); stack.Push(2); stack.Push(3);

            CollectionAssert.AreEqual(new[] { 3, 2 }, StackExtensions<int>.Peek(stack, 2));
            Assert.AreEqual(3, stack.Count);
            Assert.AreEqual(3, stack.Peek());
        }

        [TestMethod]
        public void Peek_CountLargerThanStackReturnsEverything()
        {
            Stack<int> stack = new();
            stack.Push(1); stack.Push(2);

            CollectionAssert.AreEqual(new[] { 2, 1 }, StackExtensions<int>.Peek(stack, 10));
            Assert.AreEqual(0, StackExtensions<int>.Peek(new Stack<int>(), 3).Count);
            Assert.AreEqual(0, StackExtensions<int>.Peek(stack, 0).Count);
        }

        [TestMethod]
        public void Bounds_UseTheirOwnPointSpace()
        {
            MappingVector2[] points =
            [
                new(new Vector2(0, 10), new Vector2(100, -5)),
                new(new Vector2(4, 2), new Vector2(120, -9)),
                new(new Vector2(-3, 7), new Vector2(90, 1)),
            ];

            Rectangle control = points.ControlBounds();
            Assert.AreEqual(-3, control.Left);
            Assert.AreEqual(4, control.Right);
            Assert.AreEqual(2, control.Bottom);
            Assert.AreEqual(10, control.Top);

            Rectangle mapped = points.MappedBounds();
            Assert.AreEqual(90, mapped.Left);
            Assert.AreEqual(120, mapped.Right);
            Assert.AreEqual(-9, mapped.Bottom);
            Assert.AreEqual(1, mapped.Top);
        }

        [TestMethod]
        public void Bounds_SinglePointIsDegenerateRectangle()
        {
            MappingVector2[] points = [new(new Vector2(5, 6), new Vector2(7, 8))];
            Rectangle control = points.ControlBounds();
            Assert.AreEqual(5, control.Left);
            Assert.AreEqual(5, control.Right);
            Assert.AreEqual(6, control.Bottom);
            Assert.AreEqual(6, control.Top);
        }

        [TestMethod]
        public void Bounds_ContainEveryPointAtVolumeMagnitudes()
        {
            Gen<double> coord = Gen.Choose(-4_000_000, 4_000_000).Select(i => i / 4.0);
            Gen<MappingVector2> vec = from a in coord from b in coord from c in coord from d in coord
                                      select new MappingVector2(new Vector2(a, b), new Vector2(c, d));
            var arb = Arb.From(Gen.NonEmptyListOf(vec).Select(l => l.ToArray()));

            Prop.ForAll(arb, pts =>
            {
                Rectangle cb = pts.ControlBounds();
                Rectangle mb = pts.MappedBounds();
                return pts.All(p => p.ControlPoint.X >= cb.Left && p.ControlPoint.X <= cb.Right && p.ControlPoint.Y >= cb.Bottom && p.ControlPoint.Y <= cb.Top
                                 && p.MappedPoint.X >= mb.Left && p.MappedPoint.X <= mb.Right && p.MappedPoint.Y >= mb.Bottom && p.MappedPoint.Y <= mb.Top)
                    && pts.Any(p => p.ControlPoint.X == cb.Left) && pts.Any(p => p.ControlPoint.X == cb.Right)
                    && pts.Any(p => p.MappedPoint.Y == mb.Bottom) && pts.Any(p => p.MappedPoint.Y == mb.Top);
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void SetEquals_IgnoresOrderAndDuplicates()
        {
            MappingVector2 a = new(new Vector2(0, 0), new Vector2(1, 2));
            MappingVector2 b = new(new Vector2(1, 1), new Vector2(3, 4));
            MappingVector2 c = new(new Vector2(2, 2), new Vector2(5, 6));

            Assert.IsTrue(new List<MappingVector2> { a, b, c }.SetEquals(new List<MappingVector2> { c, a, b }));
            Assert.IsTrue(new List<MappingVector2> { a, b }.SetEquals(new List<MappingVector2> { b, a, b }));
            Assert.IsFalse(new List<MappingVector2> { a, b }.SetEquals(new List<MappingVector2> { a, c }));
            Assert.IsFalse(new List<MappingVector2> { a, b }.SetEquals(new List<MappingVector2> { a }));
            Assert.IsTrue(new List<MappingVector2>().SetEquals(new List<MappingVector2>()));
        }

        [TestMethod]
        public void SetEquals_ComparesMappedPointsOnly()
        {
            MappingVector2 a = new(new Vector2(0, 0), new Vector2(1, 2));
            MappingVector2 sameMappedOtherControl = new(new Vector2(9, 9), new Vector2(1, 2));
            Assert.IsTrue(new List<MappingVector2> { a }.SetEquals(new List<MappingVector2> { sameMappedOtherControl }));
        }
    }
}
