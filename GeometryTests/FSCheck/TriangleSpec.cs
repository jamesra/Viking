using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class TriangleSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), CoreArbitraries.ArbVector2(), (t, offset) =>
                    (Triangle)t.Translate(offset).Translate(-offset) == t),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void BoundingBoxContainsVerticesAndAreaIsNonNegative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), t =>
                    t.BoundingBox.Covers(t.P1) &&
                    t.BoundingBox.Covers(t.P2) &&
                    t.BoundingBox.Covers(t.P3) &&
                    t.Area >= 0),
                nameof(BoundingBoxContainsVerticesAndAreaIsNonNegative));

        [TestMethod]
        public void GetRelationPartitionsPoints() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), CoreArbitraries.ArbVector2(), (t, p) =>
                {
                    ShapeRelation rel = t.GetRelation((IPoint2D)p);
                    bool exclusive = rel is ShapeRelation.None or ShapeRelation.Contained or ShapeRelation.Touching or ShapeRelation.Intersecting;
                    return exclusive && (t.Contains(p) == rel.IsContains()) &&
                           (t.Covers(p) == rel.IsCovers());
                }),
                nameof(GetRelationPartitionsPoints));

        [TestMethod]
        public void IntersectsIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), CoreArbitraries.ArbTriangle(),
                    (a, b) => a.Intersects((IShape2D)b) == b.Intersects((IShape2D)a)),
                nameof(IntersectsIsSymmetric));

        [TestMethod]
        public void CentroidIsContained() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), t =>
                    t.GetRelation((IPoint2D)t.Centroid) == ShapeRelation.Contained && t.Contains(t.Centroid)),
                nameof(CentroidIsContained));

        [TestMethod]
        public void BarycentricReconstructsVerticesAndCentroid() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), t =>
                {
                    Vector2 bc = t.Barycentric(t.Centroid);
                    Vector2 rebuilt = t.BaryToVector(bc);
                    return Vector2.Distance(rebuilt, t.Centroid) < 0.1 &&
                           Math.Abs(bc.X - (1.0 / 3.0)) < 0.1 &&
                           Math.Abs(bc.Y - (1.0 / 3.0)) < 0.1;
                }),
                nameof(BarycentricReconstructsVerticesAndCentroid));

        [TestMethod]
        public void CircumcircleCoversVertices() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), t =>
                {
                    Circle c = t.Circle;
                    return c.Covers(t.P1) && c.Covers(t.P2) && c.Covers(t.P3);
                }),
                nameof(CircumcircleCoversVertices));

        [TestMethod]
        public void AnglesSumToPi() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), t =>
                    Math.Abs(t.Angles.Sum() - Math.PI) < 1e-6),
                nameof(AnglesSumToPi));

        [TestMethod]
        public void WindingIsStableUnderTranslate() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), CoreArbitraries.ArbVector2(), (t, offset) =>
                    t.Winding == ((Triangle)t.Translate(offset)).Winding &&
                    t.Winding != RotationDirection.Colinear),
                nameof(WindingIsStableUnderTranslate));

        [TestMethod]
        public void IntersectsCircleIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), CoreArbitraries.ArbCircle(),
                    (t, c) => t.Intersects((IShape2D)c) == c.Intersects((IShape2D)t)),
                nameof(IntersectsCircleIsSymmetric));

        [TestMethod]
        public void EqualsIsReflexiveAndAgreesWithOperator() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbTriangle(), t =>
                    t.Equals(t) && t == new Triangle(t.P1, t.P2, t.P3) &&
                    t.GetHashCode() == new Triangle(t.P1, t.P2, t.P3).GetHashCode()),
                nameof(EqualsIsReflexiveAndAgreesWithOperator));
    }
}
