using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    [TestClass]
    public class Vector3Spec
    {
        [TestMethod]
        public void AdditionIsCommutative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), CoreArbitraries.ArbVector3(),
                    (a, b) => a + b == b + a),
                nameof(AdditionIsCommutative));

        [TestMethod]
        public void AdditionIsAssociative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), CoreArbitraries.ArbVector3(), CoreArbitraries.ArbVector3(),
                    (a, b, c) => (a + b) + c == a + (b + c)),
                nameof(AdditionIsAssociative));

        [TestMethod]
        public void ZeroIsAdditiveIdentity() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), a => a + Vector3.Zero == a),
                nameof(ZeroIsAdditiveIdentity));

        [TestMethod]
        public void NegationInvertsAddition() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), a => a + (-a) == Vector3.Zero),
                nameof(NegationInvertsAddition));

        [TestMethod]
        public void ScalarMultiplyDistributes() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbScalar(), CoreArbitraries.ArbVector3(), CoreArbitraries.ArbVector3(),
                    (s, a, b) => (a + b) * s == (a * s) + (b * s)),
                nameof(ScalarMultiplyDistributes));

        [TestMethod]
        public void NormalizeUnitLengthOrZero() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), a =>
                {
                    Vector3 n = a.Normalize();
                    if (Vector3.Magnitude(a) == 0)
                        return n == a;
                    return Tolerance.AreClose(Vector3.Magnitude(n), 1.0);
                }),
                nameof(NormalizeUnitLengthOrZero));

        [TestMethod]
        public void DotAndCrossIdentities() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), CoreArbitraries.ArbVector3(), (a, b) =>
                {
                    bool dotSym = Tolerance.AreClose(Vector3.Dot(a, b), Vector3.Dot(b, a));
                    Vector3 c = Vector3.Cross(a, b);
                    bool perpA = Math.Abs(Vector3.Dot(c, a)) <= Tolerance.Epsilon;
                    bool perpB = Math.Abs(Vector3.Dot(c, b)) <= Tolerance.Epsilon;
                    Vector3 flipped = Vector3.Cross(b, a);
                    bool anti = c + flipped == Vector3.Zero;
                    return dotSym && perpA && perpB && anti;
                }),
                nameof(DotAndCrossIdentities));

        [TestMethod]
        public void DistanceAgreesWithDistanceSquared() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), CoreArbitraries.ArbVector3(), (a, b) =>
                {
                    double d = Vector3.Distance(a, b);
                    return Tolerance.AreClose(d * d, Vector3.DistanceSquared(a, b));
                }),
                nameof(DistanceAgreesWithDistanceSquared));

        [TestMethod]
        public void ComparisonOperatorsAgreeWithCompareTo() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), CoreArbitraries.ArbVector3(), (a, b) =>
                    (a < b) == (a.CompareTo(b) < 0) &&
                    (a <= b) == (a.CompareTo(b) <= 0) &&
                    (a > b) == (a.CompareTo(b) > 0) &&
                    (a >= b) == (a.CompareTo(b) >= 0)),
                nameof(ComparisonOperatorsAgreeWithCompareTo));

        [TestMethod]
        public void EqualsIsReflexiveSymmetricAndAgreesWithHash() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVector3(), CoreArbitraries.ArbVector3(), (a, b) =>
                    a.Equals(a) &&
                    a.Equals(b) == b.Equals(a) &&
                    (!a.Equals(b) || a.GetHashCode() == b.GetHashCode())),
                nameof(EqualsIsReflexiveSymmetricAndAgreesWithHash));

        [TestMethod]
        public void UnitZIs001()
        {
            Assert.AreEqual(0, Vector3.UnitZ.X);
            Assert.AreEqual(0, Vector3.UnitZ.Y);
            Assert.AreEqual(1, Vector3.UnitZ.Z);
        }

        [TestMethod]
        public void NaNNeverEquals()
        {
            Assert.IsFalse(Vector3.NaN.Equals(Vector3.NaN));
            Assert.IsFalse(Vector3.NaN.Equals(Vector3.Zero));
        }

        [TestMethod]
        public void InfinityDoesNotCompareEqual()
        {
            Vector3 inf = new(double.PositiveInfinity, 0, 0);
            Assert.IsFalse(inf.Equals(inf));
            Assert.IsFalse(inf.Equals(Vector3.Zero));
        }
    }
}
