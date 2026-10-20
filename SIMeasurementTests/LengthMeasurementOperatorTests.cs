using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SIMeasurement;
using System;

namespace SIMeasurementTests
{
    /// <summary>
    /// Scalar multiply/divide and length ratio operators on <see cref="LengthMeasurement"/>.
    /// Used when scaling distances or comparing lengths in the measure pipeline.
    /// </summary>
    [TestClass]
    public class LengthMeasurementOperatorTests
    {
        /// <summary>
        /// Two meters divided by two meters is the unitless ratio 1.
        /// </summary>
        [TestMethod]
        public void LengthRatio_SameLength_IsOne()
        {
            LengthMeasurement twoMeters = new(SILengthUnits.m, 2);
            Assert.AreEqual(1.0, twoMeters / twoMeters, 1e-12);
        }

        /// <summary>
        /// One kilometer over one meter is 1000 after converting the numerator into the denominator unit.
        /// </summary>
        [TestMethod]
        public void LengthRatio_KilometerOverMeter_IsThousand()
        {
            LengthMeasurement kilometer = new(SILengthUnits.km, 1);
            LengthMeasurement meter = new(SILengthUnits.m, 1);
            Assert.AreEqual(1000.0, kilometer / meter, 1e-9);
        }

        /// <summary>
        /// Multiplying by zero clears the scalar; dividing zero length by a scalar stays zero.
        /// </summary>
        [TestMethod]
        public void ScalarOps_ZeroLength_StaysZeroInSameUnit()
        {
            LengthMeasurement zeroNm = new(SILengthUnits.nm, 0);
            LengthMeasurement scaled = zeroNm * 42;
            Assert.AreEqual(SILengthUnits.nm, scaled.Units);
            Assert.AreEqual(0, scaled.Length);

            LengthMeasurement divided = zeroNm / 7;
            Assert.AreEqual(SILengthUnits.nm, divided.Units);
            Assert.AreEqual(0, divided.Length);
        }

        /// <summary>
        /// Scalar multiply keeps the length unit and scales the scalar, including negative factors.
        /// </summary>
        [TestMethod]
        public void ScalarMultiply_PreservesUnitAndScalesLength()
        {
            int maxUnit = Enum.GetValues(typeof(SILengthUnits)).Length - 1;

            Gen<(int Unit, double Length, double Factor)> cases =
                from unit in Gen.Choose(0, maxUnit)
                from length in Gen.Choose(-999999, 999999).Select(i => i / 1000.0)
                from factor in Gen.Choose(-999, 999).Select(i => i / 10.0)
                select (unit, length, factor);

            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 1000;

            Prop.ForAll(Arb.From(cases), c =>
            {
                LengthMeasurement original = new((SILengthUnits)c.Unit, c.Length);
                LengthMeasurement product = original * c.Factor;
                return product.Units == original.Units &&
                       Math.Abs(product.Length - c.Length * c.Factor) <= 1e-9 * (1 + Math.Abs(c.Length * c.Factor));
            }).Check(config);
        }

        /// <summary>
        /// Scalar divide keeps the unit and divides the scalar when the divisor is non-zero.
        /// </summary>
        [TestMethod]
        public void ScalarDivide_PreservesUnitAndScalesLength()
        {
            int maxUnit = Enum.GetValues(typeof(SILengthUnits)).Length - 1;

            Gen<(int Unit, double Length, double Divisor)> cases =
                from unit in Gen.Choose(0, maxUnit)
                from length in Gen.Choose(-999999, 999999).Select(i => i / 1000.0)
                from divisor in Gen.Choose(-999, 999).Where(d => d != 0).Select(i => i / 10.0)
                select (unit, length, divisor);

            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 1000;

            Prop.ForAll(Arb.From(cases), c =>
            {
                LengthMeasurement original = new((SILengthUnits)c.Unit, c.Length);
                LengthMeasurement quotient = original / c.Divisor;
                double expected = c.Length / c.Divisor;
                return quotient.Units == original.Units &&
                       Math.Abs(quotient.Length - expected) <= 1e-9 * (1 + Math.Abs(expected));
            }).Check(config);
        }

        /// <summary>
        /// A/B converts A into B's unit and returns the numeric ratio of the two scalars.
        /// </summary>
        [TestMethod]
        public void LengthRatio_MatchesConvertToSameUnitDivision()
        {
            int maxUnit = Enum.GetValues(typeof(SILengthUnits)).Length - 1;

            Gen<(int UnitA, int UnitB, double LengthA, double LengthB)> cases =
                from unitA in Gen.Choose(0, maxUnit)
                from unitB in Gen.Choose(0, maxUnit)
                from lengthA in Gen.Choose(1, 999999).Select(i => i / 1000.0)
                from lengthB in Gen.Choose(1, 999999).Select(i => i / 1000.0)
                select (unitA, unitB, lengthA, lengthB);

            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 1000;

            Prop.ForAll(Arb.From(cases), c =>
            {
                LengthMeasurement a = new((SILengthUnits)c.UnitA, c.LengthA);
                LengthMeasurement b = new((SILengthUnits)c.UnitB, c.LengthB);
                double ratio = a / b;
                LengthMeasurement aInB = a.ConvertTo(b.Units);
                double expected = aInB.Length / b.Length;
                return Math.Abs(ratio - expected) <= 1e-9 * (1 + Math.Abs(expected));
            }).Check(config);
        }

        /// <summary>
        /// B/B is 1 for any non-zero length regardless of unit.
        /// </summary>
        [TestMethod]
        public void LengthRatio_SelfIsOneWhenNonZero()
        {
            int maxUnit = Enum.GetValues(typeof(SILengthUnits)).Length - 1;

            Gen<(int Unit, double Length)> cases =
                from unit in Gen.Choose(0, maxUnit)
                from length in Gen.OneOf(
                    Gen.Choose(1, 999999).Select(i => i / 1000.0),
                    Gen.Choose(-999999, -1).Select(i => i / 1000.0))
                select (unit, length);

            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 1000;

            Prop.ForAll(Arb.From(cases), c =>
            {
                LengthMeasurement b = new((SILengthUnits)c.Unit, c.Length);
                double ratio = b / b;
                return Math.Abs(ratio - 1.0) <= 1e-12;
            }).Check(config);
        }
    }
}
