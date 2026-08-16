using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    [TestClass]
    public class PolygonSpec
    {
        [TestMethod]
        public void ClosedRingInvariant() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                    p.ExteriorRing[0] == p.ExteriorRing[p.ExteriorRing.Length - 1] &&
                    p.ExteriorRing.IsValidClosedRing()),
                nameof(ClosedRingInvariant));

        [TestMethod]
        public void ExteriorRingIsCounterClockwiseAndAreaIsNonNegative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                    !p.ExteriorRing.AreClockwise() && p.Area >= 0),
                nameof(ExteriorRingIsCounterClockwiseAndAreaIsNonNegative));

        [TestMethod]
        public void ContainsCentroid() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), p =>
                    p.Contains(p.Centroid) && p.GetRelation((IPoint2D)p.Centroid) != ShapeRelation.None),
                nameof(ContainsCentroid));

        [TestMethod]
        public void TranslatePreservesArea() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbSimplePolygon(), CoreArbitraries.ArbVector2(), (p, offset) =>
                    Tolerance.AreClose(p.Translate(offset).Area, p.Area)),
                nameof(TranslatePreservesArea));
    }

    [TestClass]
    public class PolylineSpecCore
    {
        [TestMethod]
        public void TranslatePreservesLength() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), CoreArbitraries.ArbVector2(), (line, offset) =>
                    Tolerance.AreClose(line.Translate(offset).Length, line.Length)),
                nameof(TranslatePreservesLength));

        [TestMethod]
        public void DisallowsSelfIntersectionWhenConfigured() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbOpenPolyline(), line =>
                    !line.AllowsSelfIntersection && !line.HasSelfIntersection),
                nameof(DisallowsSelfIntersectionWhenConfigured));
    }
}
