using System;
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
            LengthMeasurement meter = new LengthMeasurement(SILengthUnits.m, 1);
            LengthMeasurement millimeter = new LengthMeasurement(SILengthUnits.m, 0.001);
            LengthMeasurement kilometer = new LengthMeasurement(SILengthUnits.m, 1000);
            LengthMeasurement micrometer = new LengthMeasurement(SILengthUnits.m, .000001);

            LengthMeasurement expectMeter = LengthMeasurement.ConvertToReadableUnits(meter);
            Assert.AreEqual(expectMeter.Units, SILengthUnits.m);
            Assert.AreEqual(expectMeter.Length, 1);

            LengthMeasurement expect_mm = LengthMeasurement.ConvertToReadableUnits(millimeter);
            Assert.AreEqual(expect_mm.Units, SILengthUnits.mm);
            Assert.AreEqual(expect_mm.Length, 1);

            LengthMeasurement expect_km = LengthMeasurement.ConvertToReadableUnits(kilometer);
            Assert.AreEqual(expect_km.Units, SILengthUnits.km);
            Assert.AreEqual(expect_km.Length, 1);

            LengthMeasurement expect_um = LengthMeasurement.ConvertToReadableUnits(micrometer);
            Assert.AreEqual(expect_um.Units, SILengthUnits.um);
            Assert.AreEqual(expect_um.Length, 1);
        }

        [TestMethod]
        public void TestConversionToNearestUnit()
        {
            LengthMeasurement meter = new LengthMeasurement(SILengthUnits.mm, 5000);
            LengthMeasurement millimeter = new LengthMeasurement(SILengthUnits.mm, 5);
            LengthMeasurement kilometer = new LengthMeasurement(SILengthUnits.mm, 5000000);

            LengthMeasurement expectMeter = LengthMeasurement.ConvertToReadableUnits(meter);
            Assert.AreEqual(expectMeter.Units, SILengthUnits.m);
            Assert.AreEqual(expectMeter.Length, 5);

            LengthMeasurement expect_mm = LengthMeasurement.ConvertToReadableUnits(millimeter);
            Assert.AreEqual(expect_mm.Units, SILengthUnits.mm);
            Assert.AreEqual(expect_mm.Length, 5);

            LengthMeasurement expect_km = LengthMeasurement.ConvertToReadableUnits(kilometer);
            Assert.AreEqual(expect_km.Units, SILengthUnits.km);
            Assert.AreEqual(expect_km.Length, 5);

            LengthMeasurement expect_nm = LengthMeasurement.ConvertToReadableUnits(SILengthUnits.nm, 303);
            Assert.AreEqual(expect_nm.Units, SILengthUnits.nm);
            Assert.AreEqual(expect_nm.Length, 303);
        }

        [TestMethod]
        public void TestConversionToNearestUndefinedUnit()
        {
            LengthMeasurement LessThanYoctometer = new LengthMeasurement(SILengthUnits.ym, 0.0002);
            LengthMeasurement BiggerThanYottameter = new LengthMeasurement(SILengthUnits.Zm, 2000000);

            LengthMeasurement expectYoctometer = LengthMeasurement.ConvertToReadableUnits(LessThanYoctometer);
            Assert.AreEqual(expectYoctometer.Units, SILengthUnits.ym);
            Assert.AreEqual(expectYoctometer.Length, 0.0002);

            LengthMeasurement expectYottameter = LengthMeasurement.ConvertToReadableUnits(BiggerThanYottameter);
            Assert.AreEqual(expectYottameter.Units, SILengthUnits.Ym);
            Assert.AreEqual(expectYottameter.Length, 2000);
        }

        [TestMethod]
        public void TestConversionToUnit()
        {
            LengthMeasurement meter = new LengthMeasurement(SILengthUnits.m, 1); 

            LengthMeasurement expect_mm = meter.ConvertTo(SILengthUnits.mm);
            Assert.AreEqual(expect_mm.Units, SILengthUnits.mm);
            Assert.AreEqual(expect_mm.Length, 1000);

            LengthMeasurement expect_um = meter.ConvertTo(SILengthUnits.um);
            Assert.AreEqual(expect_um.Units, SILengthUnits.um);
            Assert.AreEqual(expect_um.Length, 1000000);

            LengthMeasurement expect_km = meter.ConvertTo(SILengthUnits.km);
            Assert.AreEqual(expect_km.Units, SILengthUnits.km);
            Assert.AreEqual(expect_km.Length, .001);

            LengthMeasurement expect_Mm = meter.ConvertTo(SILengthUnits.Mm);
            Assert.AreEqual(expect_Mm.Units, SILengthUnits.Mm);
            Assert.AreEqual(expect_Mm.Length, .000001);
        }

        [TestMethod]
        public void TestAddSubtract()
        {
            LengthMeasurement meter = new LengthMeasurement(SILengthUnits.m, 1);
            LengthMeasurement quartermeter = new LengthMeasurement(SILengthUnits.mm, 250);

            LengthMeasurement A = meter + quartermeter;
            Assert.AreEqual(A.Units, SILengthUnits.m);
            Assert.AreEqual(A.Length, 1.25);

            LengthMeasurement B = meter - quartermeter;
            Assert.AreEqual(A.Units, SILengthUnits.mm);
            Assert.AreEqual(A.Length, 750);
        }
    }
}
