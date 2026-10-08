using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SIMeasurement;
using System;

namespace SIMeasurementTests
{
    [TestClass]
    public class SILengthTests
    {
        [TestMethod]
        public void TestSimpleConversionToNearestUnit()
        {
            LengthMeasurement meter = new(SILengthUnits.m, 1);
            LengthMeasurement millimeter = new(SILengthUnits.m, 0.001);
            LengthMeasurement kilometer = new(SILengthUnits.m, 1000);
            LengthMeasurement micrometer = new(SILengthUnits.m, .000001);

            LengthMeasurement expectMeter = LengthMeasurement.ConvertToReadableUnits(meter);
            Assert.AreEqual(SILengthUnits.m, expectMeter.Units);
            Assert.AreEqual(1, expectMeter.Length);

            LengthMeasurement expect_mm = LengthMeasurement.ConvertToReadableUnits(millimeter);
            Assert.AreEqual(SILengthUnits.mm, expect_mm.Units);
            Assert.AreEqual(1, expect_mm.Length);

            LengthMeasurement expect_km = LengthMeasurement.ConvertToReadableUnits(kilometer);
            Assert.AreEqual(SILengthUnits.km, expect_km.Units);
            Assert.AreEqual(1, expect_km.Length);

            LengthMeasurement expect_um = LengthMeasurement.ConvertToReadableUnits(micrometer);
            Assert.AreEqual(SILengthUnits.um, expect_um.Units);
            Assert.AreEqual(1, expect_um.Length);
        }

        [TestMethod]
        public void TestConversionToNearestUnit()
        {
            LengthMeasurement meter = new(SILengthUnits.mm, 5000);
            LengthMeasurement millimeter = new(SILengthUnits.mm, 5);
            LengthMeasurement kilometer = new(SILengthUnits.mm, 5000000);

            LengthMeasurement expectMeter = LengthMeasurement.ConvertToReadableUnits(meter);
            Assert.AreEqual(SILengthUnits.m, expectMeter.Units);
            Assert.AreEqual(5, expectMeter.Length);

            LengthMeasurement expect_mm = LengthMeasurement.ConvertToReadableUnits(millimeter);
            Assert.AreEqual(SILengthUnits.mm, expect_mm.Units);
            Assert.AreEqual(5, expect_mm.Length);

            LengthMeasurement expect_km = LengthMeasurement.ConvertToReadableUnits(kilometer);
            Assert.AreEqual(SILengthUnits.km, expect_km.Units);
            Assert.AreEqual(5, expect_km.Length);

            LengthMeasurement expect_nm = LengthMeasurement.ConvertToReadableUnits(SILengthUnits.nm, 303);
            Assert.AreEqual(SILengthUnits.nm, expect_nm.Units);
            Assert.AreEqual(303, expect_nm.Length);
        }

        [TestMethod]
        public void TestConversionToNearestUndefinedUnit()
        {
            LengthMeasurement LessThanYoctometer = new(SILengthUnits.ym, 0.0002);
            LengthMeasurement BiggerThanYottameter = new(SILengthUnits.Zm, 2000000);

            LengthMeasurement expectYoctometer = LengthMeasurement.ConvertToReadableUnits(LessThanYoctometer);
            Assert.AreEqual(SILengthUnits.ym, expectYoctometer.Units);
            Assert.AreEqual(0.0002, expectYoctometer.Length);

            LengthMeasurement expectYottameter = LengthMeasurement.ConvertToReadableUnits(BiggerThanYottameter);
            Assert.AreEqual(SILengthUnits.Ym, expectYottameter.Units);
            Assert.AreEqual(2000, expectYottameter.Length);
        }

        [TestMethod]
        public void TestConversionToUnit()
        {
            LengthMeasurement meter = new(SILengthUnits.m, 1);

            LengthMeasurement expect_mm = meter.ConvertTo(SILengthUnits.mm);
            Assert.AreEqual(SILengthUnits.mm, expect_mm.Units);
            Assert.AreEqual(1000, expect_mm.Length);

            LengthMeasurement expect_um = meter.ConvertTo(SILengthUnits.um);
            Assert.AreEqual(SILengthUnits.um, expect_um.Units);
            Assert.AreEqual(1000000, expect_um.Length);

            LengthMeasurement expect_km = meter.ConvertTo(SILengthUnits.km);
            Assert.AreEqual(SILengthUnits.km, expect_km.Units);
            Assert.AreEqual(.001, expect_km.Length);

            LengthMeasurement expect_Mm = meter.ConvertTo(SILengthUnits.Mm);
            Assert.AreEqual(SILengthUnits.Mm, expect_Mm.Units);
            Assert.AreEqual(.000001, expect_Mm.Length);
        }

        [TestMethod]
        public void TestAddSubtract()
        {
            LengthMeasurement meter = new(SILengthUnits.m, 1);
            LengthMeasurement quartermeter = new(SILengthUnits.mm, 250);

            LengthMeasurement A = meter + quartermeter;
            Assert.AreEqual(SILengthUnits.m, A.Units);
            Assert.AreEqual(1.25, A.Length);

            LengthMeasurement B = meter - quartermeter;
            Assert.AreEqual(SILengthUnits.mm, B.Units);
            Assert.AreEqual(750, B.Length);
        }

        /// <summary>
        /// Any positive length whose readable form lies inside the defined SI prefixes converts to the
        /// unit that puts its scalar in [1, 1000), from any starting unit. 3-digit scalars such as
        /// 303 nm, and scalars below 1 such as 0.75 m, used to land in the wrong unit.
        /// </summary>
        [TestMethod]
        public void ConvertToReadableUnitsPutsScalarInOneToOneThousand()
        {
            int maxUnit = Enum.GetValues(typeof(SILengthUnits)).Length - 1;

            // Scalars stay at least 0.001 below 1000 so Log10 rounding at the upper edge cannot hop a unit.
            // Exact powers of 1000 (scalar 1) are weighted up because Log10 rounding there decides the unit.
            Gen<int> milliScalar = Gen.Frequency(
                Tuple.Create(1, Gen.Constant(1000)),
                Tuple.Create(3, Gen.Choose(1000, 999999)));

            Gen<(int Start, int Target, double Scalar)> cases =
                from start in Gen.Choose(0, maxUnit)
                from target in Gen.Choose(0, maxUnit)
                from milli in milliScalar
                select (start, target, milli / 1000.0);

            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 1000;

            Prop.ForAll(Arb.From(cases), c =>
            {
                double distance = c.Scalar * Math.Pow(1000, c.Target - c.Start);
                LengthMeasurement readable = LengthMeasurement.ConvertToReadableUnits((SILengthUnits)c.Start, distance);

                // Math.Pow and the reverse scaling each round once; 1e-12 relative is far above that.
                return readable.Units == (SILengthUnits)c.Target &&
                       Math.Abs(readable.Length - c.Scalar) <= 1e-12 * c.Scalar;
            }).Check(config);
        }

        /// <summary>
        /// The measure tool converts a zero distance when the drag starts (cursor still on the origin).
        /// It used to read as 1 of the input unit.
        /// </summary>
        [TestMethod]
        public void ZeroLengthReadsZeroInTheInputUnit()
        {
            foreach (SILengthUnits unit in Enum.GetValues(typeof(SILengthUnits)))
            {
                LengthMeasurement readable = LengthMeasurement.ConvertToReadableUnits(unit, 0);
                Assert.AreEqual(unit, readable.Units);
                Assert.AreEqual(0, readable.Length);
            }

            LengthMeasurement dragStart = new(SILengthUnits.nm, 0 * 2.18);
            Assert.AreEqual(0, LengthMeasurement.ConvertToReadableUnits(dragStart).Length);
        }

        /// <summary>
        /// Equal lengths subtract to zero and a larger subtrahend gives a negative length, instead of +1.
        /// </summary>
        [TestMethod]
        public void SubtractionReadsZeroAndNegativeDifferences()
        {
            LengthMeasurement meter = new(SILengthUnits.m, 1);
            LengthMeasurement quartermeter = new(SILengthUnits.mm, 250);

            LengthMeasurement zero = meter - meter;
            Assert.AreEqual(SILengthUnits.m, zero.Units);
            Assert.AreEqual(0, zero.Length);

            LengthMeasurement negative = quartermeter - meter;
            Assert.AreEqual(SILengthUnits.mm, negative.Units);
            Assert.AreEqual(-750, negative.Length, 1e-9);
        }

        /// <summary>
        /// A negative length converts to the same unit as its absolute value and keeps its sign.
        /// </summary>
        [TestMethod]
        public void ConvertToReadableUnitsOfNegativeMirrorsPositive()
        {
            int maxUnit = Enum.GetValues(typeof(SILengthUnits)).Length - 1;

            Gen<(int Start, int Exponent, double Mantissa)> cases =
                from start in Gen.Choose(0, maxUnit)
                from exponent in Gen.Choose(-60, 60)
                from milli in Gen.Choose(1000, 9999)
                select (start, exponent, milli / 1000.0);

            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 1000;

            Prop.ForAll(Arb.From(cases), c =>
            {
                double distance = c.Mantissa * Math.Pow(10, c.Exponent);
                LengthMeasurement positive = LengthMeasurement.ConvertToReadableUnits((SILengthUnits)c.Start, distance);
                LengthMeasurement negative = LengthMeasurement.ConvertToReadableUnits((SILengthUnits)c.Start, -distance);

                return negative.Units == positive.Units && negative.Length == -positive.Length;
            }).Check(config);
        }
    }
}
