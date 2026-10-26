using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.ViewModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// The width profile of a link between locations on different sections: it narrows from each marker to a waist.
    /// </summary>
    [TestClass]
    public class LocationLinkPinchTests
    {
        private const double Tolerance = 1e-9;

        [TestMethod]
        public void ALinkStartsAsWideAsEachMarker()
        {
            Assert.AreEqual(10, LocationLinkView.HalfWidthAt(0, 200, 10, 6, 2), Tolerance);
            Assert.AreEqual(6, LocationLinkView.HalfWidthAt(200, 200, 10, 6, 2), Tolerance);
        }

        [TestMethod]
        public void EachSideReachesTheWaistOneDiameterFromItsCentre()
        {
            Assert.AreEqual(2, LocationLinkView.HalfWidthAt(20, 200, 10, 6, 2), Tolerance, "two radii of A from A");
            Assert.AreEqual(2, LocationLinkView.HalfWidthAt(200 - 12, 200, 10, 6, 2), Tolerance, "two radii of B from B");
            Assert.AreEqual(2, LocationLinkView.HalfWidthAt(100, 200, 10, 6, 2), Tolerance, "the middle stays at the waist");
            Assert.AreEqual(6, LocationLinkView.HalfWidthAt(10, 200, 10, 6, 2), Tolerance, "halfway down A's taper");
            Assert.AreEqual(2 + (8 * 0.5 * (1 + Math.Cos(Math.PI / 4))), LocationLinkView.HalfWidthAt(5, 200, 10, 6, 2), Tolerance,
                "the taper eases out of the marker on a cosine, above a straight ramp");
        }

        [TestMethod]
        public void TheTaperIsFlatAtTheMarkerAndAtTheNeck()
        {
            const double step = 1e-4;
            double atMarker = (LocationLinkView.HalfWidthAt(step, 200, 10, 6, 2) - LocationLinkView.HalfWidthAt(0, 200, 10, 6, 2)) / step;
            double intoWaist = (LocationLinkView.HalfWidthAt(20, 200, 10, 6, 2) - LocationLinkView.HalfWidthAt(20 - step, 200, 10, 6, 2)) / step;
            double atCloseNeck = (LocationLinkView.HalfWidthAt(6 + step, 12, 6, 6, 1) - LocationLinkView.HalfWidthAt(6 - step, 12, 6, 6, 1)) / (2 * step);

            Assert.AreEqual(0, atMarker, 1e-3);
            Assert.AreEqual(0, intoWaist, 1e-3);
            Assert.AreEqual(0, atCloseNeck, 1e-3, "close markers meet at a rounded neck, not a corner");
        }

        [TestMethod]
        public void CloseMarkersBarelyNarrow()
        {
            double narrowest = double.MaxValue;
            for (double x = 0; x <= 12; x += 0.01)
                narrowest = System.Math.Min(narrowest, LocationLinkView.HalfWidthAt(x, 12, 6, 6, 1));

            Assert.IsTrue(narrowest > 3, $"the tapers meet before the waist, narrowest {narrowest}");
            Assert.AreEqual(6 - (5.0 / 12 * 6), LocationLinkView.HalfWidthAt(6, 12, 6, 6, 1), Tolerance, "they meet in the middle");
        }

        [TestMethod]
        public void TheWaistIsNeverWiderThanTheSmallerMarker()
        {
            Assert.AreEqual(10, LocationLinkView.HalfWidthAt(0, 100, 10, 3, 8), Tolerance, "the larger marker still sets its end");
            Assert.AreEqual(3, LocationLinkView.HalfWidthAt(50, 100, 10, 3, 8), Tolerance);
            Assert.AreEqual(3, LocationLinkView.HalfWidthAt(100, 100, 10, 3, 8), Tolerance);
        }

        /// <summary>
        /// Del's fade-in scales a link's alpha through its Alpha setter, which used to discard the colour SetAlpha returned.
        /// </summary>
        [TestMethod]
        public void SettingALinksAlphaChangesOnlyItsAlpha()
        {
            // The constructors read the location store; the colour is all this test needs.
            LocationLinkView link = (LocationLinkView)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(LocationLinkView));
            link.Color = new Microsoft.Xna.Framework.Color((byte)200, (byte)100, (byte)50, (byte)64);

            link.Alpha = 0.125f;
            Assert.AreEqual(32, link.Color.A, 1);
            Assert.AreEqual(200, link.Color.R, "only the alpha changes");
        }
    }
}
