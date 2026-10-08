using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class Shape2DCollectionSpec
    {
        [TestMethod]
        public void AreaIsSumOfChildren() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbShapeCollection(), c =>
                    Tolerance.AreClose(c.Area, c.Geometries.Sum(g => g.Area))),
                nameof(AreaIsSumOfChildren));

        [TestMethod]
        public void BoundingBoxCoversEachChild() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbShapeCollection(), c =>
                    c.Geometries.All(g => c.BoundingBox.Covers(g.BoundingBox))),
                nameof(BoundingBoxCoversEachChild));

        [TestMethod]
        public void ContainsCoversIntersectsMatchAnyChild() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbShapeCollection(), CoreArbitraries.ArbVector2(), CoreArbitraries.ArbCircle(),
                    (c, p, other) =>
                    {
                        bool contains = c.Contains((IPoint2D)p) == c.Geometries.Any(g => g.Contains((IPoint2D)p));
                        bool covers = c.Covers((IPoint2D)p) == c.Geometries.Any(g => g.Covers((IPoint2D)p));
                        bool intersects = c.Intersects((IShape2D)other) == c.Geometries.Any(g => g.Intersects((IShape2D)other));
                        return contains && covers && intersects;
                    }),
                nameof(ContainsCoversIntersectsMatchAnyChild));

        [TestMethod]
        public void TranslateMapsEachChild() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbShapeCollection(), CoreArbitraries.ArbVector2(), (c, offset) =>
                {
                    Shape2DCollection moved = (Shape2DCollection)c.Translate(offset);
                    if (moved.Geometries.Count != c.Geometries.Count)
                        return false;
                    for (int i = 0; i < c.Geometries.Count; i++)
                    {
                        if (!moved.Geometries[i].Equals(c.Geometries[i].Translate(offset)))
                            return false;
                    }

                    return true;
                }),
                nameof(TranslateMapsEachChild));

        /// <summary>
        /// Equality through the <see cref="IShape2D"/> overload (what <c>List&lt;IShape2D&gt;</c>,
        /// <c>EqualityComparer&lt;IShape2D&gt;.Default</c>, and WKT round trips call) must reach the
        /// child-wise comparison. It once re-entered itself and overflowed the stack.
        /// </summary>
        [TestMethod]
        public void EqualsThroughIShape2DMatchesChildCopy() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbShapeCollection(), c =>
                    c.Equals((IShape2D)new Shape2DCollection(c.Geometries.ToList()))),
                nameof(EqualsThroughIShape2DMatchesChildCopy));

        [TestMethod]
        public void EqualsThroughIShape2DRejectsExtraChild() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbShapeCollection(), CoreArbitraries.ArbShapeCollection(), (c, d) =>
                {
                    Shape2DCollection longer = new(c.Geometries.ToList());
                    longer.Add(d.Geometries[0]);
                    return !c.Equals((IShape2D)longer) && !longer.Equals((IShape2D)c);
                }),
                nameof(EqualsThroughIShape2DRejectsExtraChild));

        [TestMethod]
        public void EqualsThroughIShape2DRejectsMovedChild() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbShapeCollection(), Arb.From(Gen.Choose(0, 2)), (c, pick) =>
                {
                    int i = pick % c.Geometries.Count;
                    var children = c.Geometries.ToList();
                    children[i] = children[i].Translate(new Vector2(1, 0));
                    return !c.Equals((IShape2D)new Shape2DCollection(children));
                }),
                nameof(EqualsThroughIShape2DRejectsMovedChild));

        /// <summary>
        /// The MULTILINESTRING shape FromWKT returns: a nested collection and a non-collection argument.
        /// </summary>
        [TestMethod]
        public void EqualsThroughIShape2DExamples()
        {
            Polyline a = new([new(30, 10), new(10, 30), new(40, 40)]);
            Polyline b = new([new(-5, 3), new(-8, -2)]);
            Shape2DCollection ab = new([a, b]);
            Shape2DCollection abCopy = new([a, b]);
            Shape2DCollection ba = new([b, a]);

            Assert.IsTrue(ab.Equals((IShape2D)abCopy));
            Assert.IsFalse(ab.Equals((IShape2D)ba));
            Assert.IsFalse(ab.Equals((IShape2D)a));
            Assert.IsFalse(ab.Equals((IShape2D)null));
            Assert.IsTrue(new Shape2DCollection([ab, a]).Equals((IShape2D)new Shape2DCollection([abCopy, a])));
            Assert.IsTrue(new System.Collections.Generic.List<IShape2D> { a, ab }.Contains(abCopy));
        }
    }
}
