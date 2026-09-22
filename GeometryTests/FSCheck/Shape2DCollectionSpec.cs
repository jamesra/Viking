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
    }
}
