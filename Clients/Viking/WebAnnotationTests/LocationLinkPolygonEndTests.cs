using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using VikingXNAGraphics;
using WebAnnotation.ViewModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// A link to a polygon on the viewed section leaves the polygon's outline, not the circle made from its centroid and
    /// area, and is no wider than the cell is thick there. Also pins the 350 ms hover fade and the arrow glyph drawn beside
    /// the end labels.
    /// </summary>
    [TestClass]
    public class LocationLinkPolygonEndTests
    {
        private const double Tolerance = 1e-6;

        private static Polygon Rectangle(double minX, double minY, double maxX, double maxY) => new(new[]
        {
            new Vector2(minX, minY), new Vector2(maxX, minY), new Vector2(maxX, maxY), new Vector2(minX, maxY), new Vector2(minX, minY),
        });

        /// <summary>Where the link starts: the far side of the end circle, toward the partner.</summary>
        private static Vector2 StartOf(Circle end, Vector2 partner)
        {
            Vector2 offset = partner - end.Center;
            return end.Center + (offset / Math.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y)) * end.Radius);
        }

        [TestMethod]
        public void ALongCellIsLeftAtItsFarEndNotFromInsideIt()
        {
            Vector2 partner = new(200, 0);
            Circle? end = LocationLinkView.AnchorOnOutline(Rectangle(0, -10, 100, 10), partner);

            Assert.IsNotNull(end);
            Assert.AreEqual(100, StartOf(end.Value, partner).X, Tolerance, "starts at the short side");
            Assert.AreEqual(10, end.Value.Radius, Tolerance, "as wide as the cell is thick");
        }

        [TestMethod]
        public void AThinCellCrossedSideOnLeavesNoGapAndCapsTheWidth()
        {
            Vector2 partner = new(50, 100);
            Circle? end = LocationLinkView.AnchorOnOutline(Rectangle(0, 0, 100, 4), partner);

            Assert.IsNotNull(end);
            Assert.AreEqual(4, StartOf(end.Value, partner).Y, Tolerance, "starts on the outline");
            Assert.AreEqual(2, end.Value.Radius, Tolerance, "half the cell's thickness");
        }

        /// <summary>The centroid of a C lies in its gap; the link still leaves the arm it crosses last.</summary>
        [TestMethod]
        public void ACShapedCellIsLeftFromItsArm()
        {
            Polygon c = new(new[]
            {
                new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 10), new Vector2(10, 10), new Vector2(10, 90),
                new Vector2(100, 90), new Vector2(100, 100), new Vector2(0, 100), new Vector2(0, 0),
            });
            Vector2 partner = new(-100, 50);
            Circle? end = LocationLinkView.AnchorOnOutline(c, partner);

            Assert.IsNotNull(end);
            Assert.AreEqual(0, StartOf(end.Value, partner).X, Tolerance, "leaves the back of the C");
            Assert.AreEqual(5, end.Value.Radius, Tolerance, "the back is 10 thick");
        }

        [TestMethod]
        public void AHoleDoesNotStopTheLinkLeavingTheOuterEdge()
        {
            Polygon ring = new(
                new[] { new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100), new Vector2(0, 0) },
                new List<Vector2[]> { new[] { new Vector2(40, 40), new Vector2(60, 40), new Vector2(60, 60), new Vector2(40, 60), new Vector2(40, 40) } });
            Vector2 partner = new(300, 50);
            Circle? end = LocationLinkView.AnchorOnOutline(ring, partner);

            Assert.IsNotNull(end);
            Assert.AreEqual(100, StartOf(end.Value, partner).X, Tolerance);
            Assert.AreEqual(20, end.Value.Radius, Tolerance, "the wall between the hole and the edge is 40 thick");
        }

        [TestMethod]
        public void APartnerInsideTheOutlineGivesNoAnchor()
        {
            Assert.IsNull(LocationLinkView.AnchorOnOutline(Rectangle(0, 0, 100, 100), new Vector2(60, 50)));
            Assert.IsNull(LocationLinkView.AnchorOnOutline(Rectangle(0, 0, 100, 100), new Vector2(50, 50)), "on the centroid");
        }

        [TestMethod]
        public void TheHoverLookTakes350MillisecondsEachWay()
        {
            Assert.AreEqual(350, LocationLinkView.HoverFadeDuration.TotalMilliseconds);
            Assert.AreEqual(0.5f, LocationLinkView.HoverAmountAt(0f, true, TimeSpan.FromMilliseconds(175)), 1e-6);
            Assert.AreEqual(1f, LocationLinkView.HoverAmountAt(0f, true, TimeSpan.FromMilliseconds(400)), 1e-6);
            Assert.AreEqual(0.5f, LocationLinkView.HoverAmountAt(1f, false, TimeSpan.FromMilliseconds(175)), 1e-6);
            Assert.AreEqual(0f, LocationLinkView.HoverAmountAt(1f, false, TimeSpan.FromMilliseconds(400)), 1e-6);
        }

        /// <summary>A fade reversed partway starts from where it was, not from the far end.</summary>
        [TestMethod]
        public void AReversedHoverFadeStartsFromWhereItWas() =>
            Assert.AreEqual(0.2f, LocationLinkView.HoverAmountAt(0.3f, false, TimeSpan.FromMilliseconds(35)), 1e-6);

        [TestMethod]
        public void ALinkSettledAfterItsFadeReportsNoFade()
        {
            LocationLinkView link = LocationLinkVisibilityTests.CreateLink();
            Assert.AreEqual(0f, link.HoverAmount, "resting");
            Assert.IsFalse(link.IsHoverFading);

            link.IsMouseOver = true;
            Assert.IsTrue(link.IsHoverFading, "fading in just after the mouse arrives");
        }

        [TestMethod]
        public void TheArrowGlyphIsClearAroundASolidHeadAndStem()
        {
            int size = ArrowGlyph.Size;
            float[] mask = ArrowGlyph.AlphaMask(size);
            float At(double x, double y) => mask[((int)(y * size) * size) + (int)(x * size)];

            Assert.AreEqual(0f, At(0.02, 0.02), "top left");
            Assert.AreEqual(0f, At(0.97, 0.97), "bottom right");
            Assert.AreEqual(0f, At(0.1, 0.8), "beside the stem");
            Assert.AreEqual(1f, At(0.5, 0.35), "head");
            Assert.AreEqual(1f, At(0.5, 0.8), "stem");
        }
    }
}
