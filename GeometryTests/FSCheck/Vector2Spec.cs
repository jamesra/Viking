using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    [TestClass]
    public class Vector2Spec
    {
        [TestMethod]
        public void AdditionIsCommutative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(),
                    (a, b) => a + b == b + a),
                nameof(AdditionIsCommutative));

        [TestMethod]
        public void AdditionIsAssociative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(),
                    (a, b, c) => (a + b) + c == a + (b + c)),
                nameof(AdditionIsAssociative));

        [TestMethod]
        public void ZeroIsAdditiveIdentity() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), a => a + Vector2.Zero == a && Vector2.Zero + a == a),
                nameof(ZeroIsAdditiveIdentity));

        [TestMethod]
        public void NegationInvertsAddition() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), a => a + (-a) == Vector2.Zero),
                nameof(NegationInvertsAddition));

        [TestMethod]
        public void ScalarMultiplyDistributes() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbScalar(), CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(),
                    (s, a, b) => (a + b) * s == (a * s) + (b * s)),
                nameof(ScalarMultiplyDistributes));

        [TestMethod]
        public void NormalizeUnitLengthOrNearZero() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), a =>
                {
                    Vector2 n = a.Normalize();
                    if (a.Magnitude <= Tolerance.Epsilon)
                        return n == a;
                    return Tolerance.AreClose(n.Magnitude, 1.0);
                }),
                nameof(NormalizeUnitLengthOrNearZero));

        [TestMethod]
        public void DotIsSymmetricAndMatchesMagnitudeSquared() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(), (a, b) =>
                {
                    bool symmetric = Tolerance.AreClose(Vector2.Dot(a, b), Vector2.Dot(b, a));
                    bool mag = Tolerance.AreClose(Vector2.Dot(a, a), a.Magnitude * a.Magnitude);
                    return symmetric && mag;
                }),
                nameof(DotIsSymmetricAndMatchesMagnitudeSquared));

        [TestMethod]
        public void CrossViaRotate90IsAntisymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(), (a, b) =>
                {
                    double ab = Vector2.Dot(Vector2.Rotate90(a), b);
                    double ba = Vector2.Dot(Vector2.Rotate90(b), a);
                    return Tolerance.AreClose(ab, -ba);
                }),
                nameof(CrossViaRotate90IsAntisymmetric));

        [TestMethod]
        public void Rotate90IsPerpendicularAndPreservesLength() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), a =>
                {
                    Vector2 r = Vector2.Rotate90(a);
                    return Tolerance.AreClose(Vector2.Dot(a, r), 0) &&
                           Tolerance.AreClose(r.Magnitude, a.Magnitude);
                }),
                nameof(Rotate90IsPerpendicularAndPreservesLength));

        [TestMethod]
        public void DistanceAgreesWithDistanceSquared() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(), (a, b) =>
                {
                    double d = Vector2.Distance(a, b);
                    return Tolerance.AreClose(d * d, Vector2.DistanceSquared(a, b));
                }),
                nameof(DistanceAgreesWithDistanceSquared));

        [TestMethod]
        public void EqualsIsReflexiveSymmetricAndAgreesWithHash() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(), (a, b) =>
                {
                    bool reflexive = a.Equals(new Vector2(a.X, a.Y)) && a == new Vector2(a.X, a.Y);
                    bool symmetric = a.Equals(b) == b.Equals(a);
                    bool hash = !a.Equals(b) || a.GetHashCode() == b.GetHashCode();
                    return reflexive && symmetric && hash;
                }),
                nameof(EqualsIsReflexiveSymmetricAndAgreesWithHash));

        [TestMethod]
        public void NaNNeverEquals()
        {
            Assert.IsFalse(Vector2.NaN.Equals(Vector2.NaN));
            Assert.IsFalse(Vector2.NaN.Equals(Vector2.Zero));
            Assert.IsFalse(Vector2.Equals(Vector2.NaN, Vector2.NaN));
        }

        [TestMethod]
        public void InfinityDoesNotCompareEqual()
        {
            Vector2 inf = new(double.PositiveInfinity, 0);
            Assert.IsFalse(inf.Equals(inf));
            Assert.IsFalse(inf.Equals(Vector2.Zero));
            Assert.IsFalse(new Vector2(double.PositiveInfinity, double.PositiveInfinity)
                .Equals(new Vector2(double.PositiveInfinity, double.PositiveInfinity)));
        }

        [TestMethod]
        public void AsShapeContainsOnlyCoincidence() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), CoreArbitraries.ArbVector2(), (a, b) =>
                {
                    IShape2D shape = a;
                    ShapeRelation rel = shape.GetRelation((IPoint2D)b);
                    bool coincidence = a.Equals(b);
                    return coincidence
                        ? rel == ShapeRelation.Contained && shape.Contains((IPoint2D)b) && shape.Covers((IPoint2D)b)
                        : rel == ShapeRelation.None && !shape.Contains((IPoint2D)b) && !shape.Covers((IPoint2D)b);
                }),
                nameof(AsShapeContainsOnlyCoincidence));

        [TestMethod]
        public void AsShapeIntersectsViaOtherCovers() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), CoreArbitraries.ArbCircle(), (p, c) =>
                    ((IShape2D)p).Intersects(c) == c.Covers((IPoint2D)p)),
                nameof(AsShapeIntersectsViaOtherCovers));

        [TestMethod]
        public void RotateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector2(), a =>
                    Vector2.Distance(a, a.Rotate(0.3).Rotate(-0.3)) < 1e-8),
                nameof(RotateIsInvertible));
    }
}
