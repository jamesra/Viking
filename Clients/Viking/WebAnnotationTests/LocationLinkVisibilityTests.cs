using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.ViewModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// A location link (the hourglass between two markers) is drawn as strongly as the Link visibility preference asks,
    /// strongest next to its markers, and each end is labelled with the ID of the location at the other end and an arrow
    /// pointing to it. Pins the opacity rules and the label text.
    /// </summary>
    [TestClass]
    public class LocationLinkVisibilityTests
    {
        [TestMethod]
        public void TheDrawnOpacityIsTheVisibilityPreference()
        {
            Assert.AreEqual(0.6f, LocationLinkView.DrawnAlpha(1f, 0.6f), 1e-6);
            Assert.AreEqual(1f, LocationLinkView.DrawnAlpha(1f, 1f), 1e-6);
            Assert.AreEqual(0.1f, LocationLinkView.DrawnAlpha(1f, 0.1f), 1e-6);
        }

        /// <summary>The mouse-over effect halves the link's stored alpha; the drawn opacity must follow it.</summary>
        [TestMethod]
        public void HoveringHalvesTheDrawnOpacity()
        {
            Assert.AreEqual(0.3f, LocationLinkView.DrawnAlpha(0.5f, 0.6f), 1e-6);
            Assert.AreEqual(0.5f, LocationLinkView.LabelAlpha(0.5f, 0.6f), 1e-6);
        }

        [TestMethod]
        public void LinksAreStoredOpaqueSoThePreferenceAloneSetsTheirOpacity()
        {
            Microsoft.Xna.Framework.Color color = LocationLinkView.GetLocationLinkColor(Microsoft.Xna.Framework.Color.Green, 1, 1, false);
            Assert.AreEqual(255, color.A);
        }

        [TestMethod]
        public void LabelsStayReadableAtLowVisibility()
        {
            Assert.AreEqual(1f, LocationLinkView.LabelAlpha(1f, 0.5f), 1e-6);
            Assert.AreEqual(0.6f, LocationLinkView.LabelAlpha(1f, 0.1f), 1e-6);
        }

        [TestMethod]
        public void TheMiddleIsFainterThanTheEndsAndTheShadowIsDrawn()
        {
            Assert.IsTrue(LocationLinkView.WaistOpacity > 0f && LocationLinkView.WaistOpacity < 0.5f);
            Assert.IsTrue(LocationLinkView.ShadowPixels >= 2f);
        }

        /// <summary>Upright text reads toward the other end, so the arrow follows the ID and points away from this end.</summary>
        [TestMethod]
        public void AnUprightLabelPutsTheArrowAfterTheID() =>
            Assert.AreEqual("34532 ->", LocationLinkView.EndLabelText(34532, upright: true));

        /// <summary>Turned-over text reads back toward this end, so the arrow comes first to still point away from it.</summary>
        [TestMethod]
        public void ATurnedOverLabelPutsTheArrowBeforeTheID() =>
            Assert.AreEqual("<- 34532", LocationLinkView.EndLabelText(34532, upright: false));

        [TestMethod]
        public void LabelsNeedRoomForBothAndTheirGaps()
        {
            double gaps = 3 * LocationLinkView.LabelGapPixels;
            Assert.IsTrue(LocationLinkView.EndLabelsFit(100 + gaps, 50, 50));
            Assert.IsFalse(LocationLinkView.EndLabelsFit(100 + gaps - 1, 50, 50));
        }
    }
}
