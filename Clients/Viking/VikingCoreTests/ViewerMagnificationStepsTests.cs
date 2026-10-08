using System;
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
    }
}
