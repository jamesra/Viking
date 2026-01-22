using Microsoft.VisualStudio.TestTools.UnitTesting;
using SIMeasurement;

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
            Assert.AreEqual(SILengthUnits.mm, A.Units);
            Assert.AreEqual(750, A.Length);
        }
    }
}
