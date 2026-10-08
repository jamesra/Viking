using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins toolbar zoom math: four clicks double or half downsample, Home snaps to power of two.
    /// </summary>
    [TestClass]
    public class ViewerMagnificationStepsTests
    {
        private const double Epsilon = 1e-9;

        [TestMethod]
        public void FourZoomOutClicks_DoublesDownsample()
        {
            double ds = 1.0;
            for (int i = 0; i < 4; i++)
                ds = ViewerMagnificationSteps.ZoomOut(ds);

            Assert.AreEqual(2.0, ds, Epsilon);
        }

        [TestMethod]
        public void FourZoomInClicks_HalvesDownsample()
        {
            double ds = 8.0;
            for (int i = 0; i < 4; i++)
                ds = ViewerMagnificationSteps.ZoomIn(ds);

            Assert.AreEqual(4.0, ds, Epsilon);
        }

        [TestMethod]
        public void ZoomInThenZoomOut_RoundTrips()
        {
            double start = 3.25;
            double after = ViewerMagnificationSteps.ZoomOut(ViewerMagnificationSteps.ZoomIn(start));
            Assert.AreEqual(start, after, Epsilon);
        }

        [TestMethod]
        public void NearestPowerOfTwo_SnapsMidpointsTowardEvenPowers()
        {
            Assert.AreEqual(4.0, ViewerMagnificationSteps.NearestPowerOfTwo(3.0), Epsilon);
            Assert.AreEqual(2.0, ViewerMagnificationSteps.NearestPowerOfTwo(1.5), Epsilon);
            Assert.AreEqual(1.0, ViewerMagnificationSteps.NearestPowerOfTwo(1.0), Epsilon);
            Assert.AreEqual(0.5, ViewerMagnificationSteps.NearestPowerOfTwo(0.7), Epsilon);
            Assert.AreEqual(0.5, ViewerMagnificationSteps.NearestPowerOfTwo(0), Epsilon);
            Assert.AreEqual(0.5, ViewerMagnificationSteps.NearestPowerOfTwo(-2), Epsilon);
        }

        [TestMethod]
        public void StepFactor_IsFourthRootOfTwo()
        {
            Assert.AreEqual(Math.Pow(2.0, 0.25), ViewerMagnificationSteps.StepFactor, Epsilon);
        }

        [TestMethod]
        public void IsValidDownsample_ZeroOrNonFinite_ReturnsFalse()
        {
            Assert.IsFalse(ViewerMagnificationSteps.IsValidDownsample(0));
            Assert.IsFalse(ViewerMagnificationSteps.IsValidDownsample(-1));
            Assert.IsFalse(ViewerMagnificationSteps.IsValidDownsample(double.NaN));
            Assert.IsFalse(ViewerMagnificationSteps.IsValidDownsample(double.PositiveInfinity));
            Assert.IsFalse(ViewerMagnificationSteps.IsValidDownsample(double.NegativeInfinity));
        }

        [TestMethod]
        public void NearestPowerOfTwo_NonFinite_ReturnsDefault()
        {
            Assert.AreEqual(0.5, ViewerMagnificationSteps.NearestPowerOfTwo(double.NaN), Epsilon);
            Assert.AreEqual(0.5, ViewerMagnificationSteps.NearestPowerOfTwo(double.PositiveInfinity), Epsilon);
            Assert.AreEqual(0.5, ViewerMagnificationSteps.NearestPowerOfTwo(double.NegativeInfinity), Epsilon);
        }

        [TestMethod]
        public void ZoomInAndZoomOut_NonFiniteOrNonPositive_LeaveInputUnchanged()
        {
            foreach (double bad in new[] { 0.0, -2.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.AreEqual(bad, ViewerMagnificationSteps.ZoomIn(bad));
                Assert.AreEqual(bad, ViewerMagnificationSteps.ZoomOut(bad));
            }
        }

        [TestMethod]
        public void ZoomInThenZoomOut_ValidPositiveFinite_RoundTrips()
        {
            var gen = Gen.Choose(1, 1_000_000).Select(i => i / 1000.0);
            Prop.ForAll(Arb.From(gen), start =>
            {
                double after = ViewerMagnificationSteps.ZoomOut(ViewerMagnificationSteps.ZoomIn(start));
                return Math.Abs(after - start) < Epsilon;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void NearestPowerOfTwo_ValidPositiveFinite_IsPowerOfTwo()
        {
            var gen = Gen.Choose(1, 1_000_000).Select(i => i / 1000.0);
            Prop.ForAll(Arb.From(gen), downsample =>
            {
                double snapped = ViewerMagnificationSteps.NearestPowerOfTwo(downsample);
                if (!ViewerMagnificationSteps.IsValidDownsample(downsample))
                    return false;

                double log2 = Math.Log(snapped, 2.0);
                return snapped > 0 && Math.Abs(log2 - Math.Round(log2)) < 1e-9;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ZoomInAndZoomOut_InvalidInput_ReturnsSameValue()
        {
            var badGen = Gen.OneOf(
                Gen.Constant(double.NaN),
                Gen.Constant(double.PositiveInfinity),
                Gen.Constant(double.NegativeInfinity),
                Gen.Choose(-1000, 0).Select(i => i / 100.0));
            Prop.ForAll(Arb.From(badGen), bad =>
            {
                double zoomIn = ViewerMagnificationSteps.ZoomIn(bad);
                double zoomOut = ViewerMagnificationSteps.ZoomOut(bad);
                return AreSameDouble(bad, zoomIn) && AreSameDouble(bad, zoomOut);
            }).QuickCheckThrowOnFailure();
        }

        private static bool AreSameDouble(double a, double b) =>
            double.IsNaN(a) && double.IsNaN(b)
            || a == b;
    }
}
