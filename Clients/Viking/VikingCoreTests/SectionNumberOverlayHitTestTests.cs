using Microsoft.VisualStudio.TestTools.UnitTesting;
using VikingXNAGraphics;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="SectionNumberOverlayView.SectionAtY"/>, used by a pen tap on the section number overlay:
    /// it must invert the label placement in <c>DrawLabels</c> so the tapped number is the one drawn there.
    /// </summary>
    [TestClass]
    public class SectionNumberOverlayHitTestTests
    {
        private const double ScreenHeight = 510;
        private const double SlotHeight = 10; // 51 slots

        /// <summary>Label centre Y as <c>DrawLabels</c> places it.</summary>
        private static double LabelY(int section, double animatedSectionPosition) =>
            ScreenHeight / 2.0 - (section - animatedSectionPosition) * SlotHeight - SlotHeight / 2.0;

        [TestMethod]
        public void LabelCentre_MapsBackToItsSection()
        {
            foreach (double animated in new[] { 100.0, 100.25, 99.5, 3.0 })
            {
                for (int section = (int)animated - 25; section <= (int)animated + 25; section++)
                    Assert.AreEqual(section, SectionNumberOverlayView.SectionAtY(LabelY(section, animated), ScreenHeight, SlotHeight, animated),
                        $"section {section} at animated position {animated}");
            }
        }

        [TestMethod]
        public void HigherSections_AreAbove()
        {
            Assert.AreEqual(100, SectionNumberOverlayView.SectionAtY(250, ScreenHeight, SlotHeight, 100));
            Assert.AreEqual(101, SectionNumberOverlayView.SectionAtY(240, ScreenHeight, SlotHeight, 100));
            Assert.AreEqual(99, SectionNumberOverlayView.SectionAtY(260, ScreenHeight, SlotHeight, 100));
            Assert.AreEqual(125, SectionNumberOverlayView.SectionAtY(0, ScreenHeight, SlotHeight, 100));
        }

        [TestMethod]
        public void PointBetweenLabels_PicksNearest()
        {
            Assert.AreEqual(100, SectionNumberOverlayView.SectionAtY(245.1, ScreenHeight, SlotHeight, 100));
            Assert.AreEqual(101, SectionNumberOverlayView.SectionAtY(244.9, ScreenHeight, SlotHeight, 100));
        }
    }
}
