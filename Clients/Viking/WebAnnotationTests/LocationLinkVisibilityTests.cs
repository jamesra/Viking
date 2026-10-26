using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.AnnotationServiceTypes;
using WebAnnotation.ViewModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// A location link (the hourglass between two markers) is drawn as strongly as the Link visibility preference asks
    /// next to its markers and fades to nothing in the middle unless the mouse is over it; what is invisible cannot be
    /// picked. Each end is labelled with the ID of the location at the other end and an arrow pointing to it, sized in
    /// volume space. Go to partner jumps to the end farther from the mouse. Pins those rules.
    /// </summary>
    [TestClass]
    public class LocationLinkVisibilityTests
    {
        private const double Tolerance = 1e-9;

        /// <summary>
        /// A link from (0,0) to (100,0) with markers of radius 10, built without the location store. Not hovered unless asked.
        /// </summary>
        internal static LocationLinkView CreateLink(bool hovered = false, long a = 1, long b = 2)
        {
            LocationLinkView link = (LocationLinkView)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(LocationLinkView));
            typeof(LocationLinkView).GetField(nameof(LocationLinkView.Key)).SetValue(link, new LocationLinkKey(a, b));
            link.A = new Circle(0, 0, 10);
            link.B = new Circle(100, 0, 10);
            link.Color = Microsoft.Xna.Framework.Color.White;
            link.IsMouseOver = hovered;
            return link;
        }

        [TestMethod]
        public void TheDrawnOpacityIsTheVisibilityPreference()
        {
            Assert.AreEqual(0.6f, LocationLinkView.DrawnAlpha(1f, 0.6f), 1e-6);
            Assert.AreEqual(1f, LocationLinkView.DrawnAlpha(1f, 1f), 1e-6);
            Assert.AreEqual(0.1f, LocationLinkView.DrawnAlpha(1f, 0.1f), 1e-6);
        }

        /// <summary>Del's fade-in scales the link's stored alpha; the drawn opacity and the labels must follow it.</summary>
        [TestMethod]
        public void AFadingLinkDrawsFainter()
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
        public void TheWaistOfALinkNotHoveredIsInvisibleAndTheShadowIsDrawn()
        {
            Assert.AreEqual(0f, LocationLinkView.WaistOpacity);
            Assert.IsTrue(LocationLinkView.ShadowPixels >= 2f);
        }

        /// <summary>The fade starts at each marker's edge and reaches the waist one radius further on.</summary>
        [TestMethod]
        public void TheFadeRunsFromEachMarkersEdgeToOneRadiusBeyondIt()
        {
            Assert.AreEqual(0, LocationLinkView.FadeDepthAt(5, 100, 10, 10), Tolerance, "inside the marker");
            Assert.AreEqual(0, LocationLinkView.FadeDepthAt(10, 100, 10, 10), Tolerance, "at the edge");
            Assert.AreEqual(0.5, LocationLinkView.FadeDepthAt(15, 100, 10, 10), Tolerance);
            Assert.AreEqual(1, LocationLinkView.FadeDepthAt(20, 100, 10, 10), Tolerance, "at the waist");
            Assert.AreEqual(1, LocationLinkView.FadeDepthAt(50, 100, 10, 10), Tolerance, "middle");
            Assert.AreEqual(0.5, LocationLinkView.FadeDepthAt(85, 100, 10, 10), Tolerance, "from B");
        }

        /// <summary>A small marker's end is measured by its own radius, so it is not already faded at its edge.</summary>
        [TestMethod]
        public void ASmallMarkersEndStartsAtFullOpacity()
        {
            Assert.AreEqual(0, LocationLinkView.FadeDepthAt(97, 100, 30, 3), Tolerance);
            Assert.AreEqual(0, LocationLinkView.FadeDepthAt(30, 100, 30, 3), Tolerance);
        }

        [TestMethod]
        public void TheInvisibleMiddleCannotBePickedUntilTheLinkIsHovered()
        {
            Vector2 nearA = new(14, 0);
            Vector2 middle = new(50, 0);

            LocationLinkView link = CreateLink();
            Assert.IsTrue(link.Contains(nearA), "the strong part next to a marker");
            Assert.IsFalse(link.Contains(middle), "the invisible middle");

            link.IsMouseOver = true;
            Assert.IsTrue(link.Contains(middle), "hovered, the whole link can be followed");
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
            // Gap 4 to A's label, the larger gap 4 between the labels, gap 2 to B's marker.
            Assert.IsTrue(LocationLinkView.EndLabelsFit(100 + 10, 50, 4, 50, 2));
            Assert.IsFalse(LocationLinkView.EndLabelsFit(100 + 9, 50, 4, 50, 2));
        }

        /// <summary>Labels are sized from the marker radius in volume space, so they zoom with the image.</summary>
        [TestMethod]
        public void LabelsAreAFixedFractionOfTheMarkerRadius() =>
            Assert.IsTrue(LocationLinkView.LabelHeightPerRadius > 0 && LocationLinkView.LabelHeightPerRadius < 0.5);

        [TestMethod]
        public void GoToPartnerPicksTheEndFartherFromTheMouse()
        {
            Vector2 a = new(0, 0);
            Vector2 b = new(100, 0);
            Assert.IsFalse(LocationLinkView.AIsPartnerOf(new Vector2(10, 5), a, b), "near A, go to B");
            Assert.IsTrue(LocationLinkView.AIsPartnerOf(new Vector2(90, -5), a, b), "near B, go to A");
        }

        /// <summary>Hovering makes a link fully visible instead of fading it as it does a location.</summary>
        [TestMethod]
        public void HoveringALinkMarksItAndLeavesItsAlpha()
        {
            LocationLinkView link = CreateLink();
            WebAnnotation.Actions.MouseOverLocationCanvasViewEffect effect = new();

            effect.viewObj = link;
            Assert.IsTrue(link.IsMouseOver);
            Assert.AreEqual(255, link.Color.A);

            effect.viewObj = null;
            Assert.IsFalse(link.IsMouseOver);
        }
    }
}
