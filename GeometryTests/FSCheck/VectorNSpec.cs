using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class VectorNSpec
    {
        [TestMethod]
        public void AdditionIsCommutative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVectorNPair(), pair => pair.Item1 + pair.Item2 == pair.Item2 + pair.Item1),
                nameof(AdditionIsCommutative));

        [TestMethod]
        public void ZeroIsAdditiveIdentity() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVectorN(), a =>
                {
                    VectorN zero = new(new double[a.DimensionCount]);
                    return a + zero == a;
                }),
                nameof(ZeroIsAdditiveIdentity));

        [TestMethod]
        public void NegationInvertsAddition() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVectorN(), a =>
                {
                    VectorN zero = new(new double[a.DimensionCount]);
                    return a + (-a) == zero;
                }),
                nameof(NegationInvertsAddition));

        [TestMethod]
        public void ScalarMultiplyDistributes() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbScalar(), CoreArbitraries.ArbVectorNPair(), (s, pair) =>
                    (pair.Item1 + pair.Item2) * s == (pair.Item1 * s) + (pair.Item2 * s)),
                nameof(ScalarMultiplyDistributes));

        [TestMethod]
        public void NormalizeUnitLengthOrZero() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVectorN(), a =>
                {
                    double mag = VectorN.Magnitude(a);
                    if (mag == 0)
                        return true;
                    return Tolerance.AreClose(VectorN.Magnitude(a.Normalize()), 1.0);
                }),
                nameof(NormalizeUnitLengthOrZero));

        [TestMethod]
        public void DistanceAgreesWithDistanceSquared() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVectorNPair(), pair =>
                {
                    double d = Math.Sqrt(VectorN.DistanceSquared(pair.Item1, pair.Item2));
                    return Tolerance.AreClose(d * d, VectorN.DistanceSquared(pair.Item1, pair.Item2));
                }),
                nameof(DistanceAgreesWithDistanceSquared));

        [TestMethod]
        public void EqualsIsReflexiveSymmetricAndAgreesWithHash() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbVectorNPair(), pair =>
                {
                    VectorN a = pair.Item1;
                    VectorN b = pair.Item2;
                    return a.Equals(a) &&
                           a.Equals(b) == b.Equals(a) &&
                           (!a.Equals(b) || a.GetHashCode() == b.GetHashCode());
                }),
                nameof(EqualsIsReflexiveSymmetricAndAgreesWithHash));

        [TestMethod]
        public void CoordsIsACopy()
        {
            VectorN v = new([1, 2, 3]);
            double[] coords = v.Coords;
            coords[0] = 99;
            Assert.AreEqual(1, v.Coords[0]);
        }

        [TestMethod]
        public void NaNNeverEquals()
        {
            VectorN nan = new([double.NaN, double.NaN]);
            Assert.IsFalse(nan.Equals(nan));
            Assert.IsFalse(nan.Equals(new VectorN([0, 0])));
        }

        [TestMethod]
        public void InfinityDoesNotCompareEqual()
        {
            VectorN inf = new([double.PositiveInfinity, 0]);
            Assert.IsFalse(inf.Equals(inf));
        }
    }
}
