using System;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="ProposalButtonLayout"/>: the accept and hide buttons share one rectangle inside the proposal and outside
    /// its circle, side by side when that fits and stacked (accept on top) when it does not, inside the circle's lower right
    /// otherwise. The size is in the image (at 1x) and the layout does not change with the zoom; only the fade does.
    /// </summary>
    [TestClass]
    public class ProposalButtonLayoutTests
    {
        private static readonly ProposalButtonOptions Options = new(SizePixels: 56, MinDiameterPixels: 12);

        private static Polygon Rectangle(double cx, double cy, double halfX, double halfY) => new(new[]
        {
            new Vector2(cx - halfX, cy - halfY), new Vector2(cx + halfX, cy - halfY), new Vector2(cx + halfX, cy + halfY),
            new Vector2(cx - halfX, cy + halfY), new Vector2(cx - halfX, cy - halfY),
        });

        private static Polygon Square(double cx, double cy, double half) => Rectangle(cx, cy, half, half);

        /// <summary>The pair's rectangle lies inside the polygon and clear of the seed circle.</summary>
        private static void AssertRectangleFits(ProposalButtonPlacement placement, Polygon polygon, Circle? seed)
        {
            (double halfX, double halfY) = ProposalButtonLayout.HalfSize(placement.Arrangement);
            Vector2 middle = new((placement.Accept.X + placement.Hide.X) / 2, (placement.Accept.Y + placement.Hide.Y) / 2);
            double hx = halfX * placement.Radius, hy = halfY * placement.Radius;
            foreach (Vector2 corner in new[]
            {
                new Vector2(middle.X - hx, middle.Y - hy), new Vector2(middle.X + hx, middle.Y - hy),
                new Vector2(middle.X + hx, middle.Y + hy), new Vector2(middle.X - hx, middle.Y + hy),
            })
            {
                Assert.IsTrue(polygon.Contains(corner), $"rectangle corner {corner} is inside the proposal");
            }

            if (seed is Circle c)
            {
                double dx = Math.Max(Math.Abs(c.Center.X - middle.X) - hx, 0), dy = Math.Max(Math.Abs(c.Center.Y - middle.Y) - hy, 0);
                Assert.IsTrue(Math.Sqrt(dx * dx + dy * dy) >= c.Radius - 1e-6, "the rectangle is clear of the circle");
            }
        }

        [TestMethod]
        public void RoomOutsideTheCircle_SideBySide_AcceptOnTheLeft()
        {
            Polygon polygon = Square(0, 0, 200);
            Circle seed = new(new Vector2(-100, 0), 60);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed, downsample: 1, Options);

            Assert.IsNotNull(placement);
            Assert.IsFalse(placement.InsideCircle);
            Assert.AreEqual(ProposalButtonArrangement.LeftToRight, placement.Arrangement);
            Assert.IsTrue(placement.Accept.X < placement.Hide.X, "accept on the left");
            Assert.AreEqual(placement.Accept.Y, placement.Hide.Y, 1e-9, "on one line");
            Assert.AreEqual(28, placement.Radius, 1e-9, "the full size, half of 56");
            Assert.AreEqual(2 * 28 * (1 + ProposalButtonLayout.GapFraction / 2), Vector2.Distance(placement.Accept, placement.Hide), 1e-9);
            AssertRectangleFits(placement, polygon, seed);
        }

        [TestMethod]
        public void ZoomChangesOnlyTheFade()
        {
            Polygon polygon = Square(0, 0, 200);
            Circle seed = new(new Vector2(-100, 0), 60);

            ProposalButtonPlacement atOne = ProposalButtonLayout.Place(polygon, seed, downsample: 1, Options);
            ProposalButtonPlacement zoomedOut = ProposalButtonLayout.Place(polygon, seed, downsample: 4.5, Options);

            Assert.AreEqual(atOne.Accept, zoomedOut.Accept, "the buttons do not move");
            Assert.AreEqual(atOne.Hide, zoomedOut.Hide);
            Assert.AreEqual(atOne.Radius, zoomedOut.Radius, "nor change size in the image");
            Assert.AreEqual(1, atOne.Fade);
            Assert.IsTrue(zoomedOut.Fade < 1, "56 / 4.5 = 12.4 px across on screen: fading");
        }

        [TestMethod]
        public void NarrowRoom_StacksWithAcceptOnTop()
        {
            // 20 wide: side by side the buttons would be under 12 px across; stacked they are 16.
            Polygon polygon = Rectangle(0, 0, 10, 200);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed: null, downsample: 1, Options);

            Assert.IsNotNull(placement);
            Assert.AreEqual(ProposalButtonArrangement.TopToBottom, placement.Arrangement);
            Assert.AreEqual(placement.Accept.X, placement.Hide.X, 1e-9, "one above the other");
            Assert.IsTrue(placement.Accept.Y > placement.Hide.Y, "accept on top (world y is up)");
            Assert.IsTrue(placement.Radius >= Options.MinDiameterPixels / 2);
            AssertRectangleFits(placement, polygon, null);
        }

        [TestMethod]
        public void SideBySideWhenItFits_EvenIfStackingWouldBeLarger()
        {
            // 60 wide: stacked would be larger, but side by side is clickable, so it wins.
            Polygon polygon = Rectangle(0, 0, 30, 200);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed: null, downsample: 1, Options);

            Assert.AreEqual(ProposalButtonArrangement.LeftToRight, placement.Arrangement);
            AssertRectangleFits(placement, polygon, null);
        }

        [TestMethod]
        public void NoRoomOutsideTheCircle_ButtonsGoInsideItsLowerRight()
        {
            // A proposal that hugs its circle: 3 units of room all round, too little for a 12 px button.
            Circle seed = new(new Vector2(0, 0), 100);
            Vector2[] ring = new Vector2[65];
            for (int i = 0; i < 64; i++)
            {
                double angle = 2 * Math.PI * i / 64;
                ring[i] = new Vector2(103 * Math.Cos(angle), 103 * Math.Sin(angle));
            }
            ring[64] = ring[0];
            Polygon polygon = new(ring);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed, downsample: 1, Options);

            Assert.IsNotNull(placement);
            Assert.IsTrue(placement.InsideCircle);
            Assert.AreEqual(ProposalButtonArrangement.LeftToRight, placement.Arrangement);
            Assert.IsTrue(placement.Accept.X < placement.Hide.X, "accept on the left, hide on the right");
            Assert.AreEqual(placement.Accept.Y, placement.Hide.Y, 1e-9);
            Assert.IsTrue(placement.Accept.Y < seed.Center.Y, "below the centre (world y is up)");
            Assert.IsTrue((placement.Accept + placement.Hide).X / 2 > seed.Center.X, "right of the centre");
            foreach (Vector2 center in new[] { placement.Accept, placement.Hide })
                Assert.IsTrue(Vector2.Distance(center, seed.Center) + placement.Radius <= seed.Radius, "inside the circle");
        }

        [TestMethod]
        public void NoSeedCircle_ButtonsUseTheWholePolygon()
        {
            Polygon polygon = Square(0, 0, 50);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed: null, downsample: 1, Options);

            Assert.IsNotNull(placement);
            Assert.IsFalse(placement.InsideCircle);
            AssertRectangleFits(placement, polygon, null);
        }

        [TestMethod]
        public void PolygonWithAHole_ButtonsAvoidTheHole()
        {
            Vector2[] exterior = Square(0, 0, 200).ExteriorRing;
            Vector2[] hole = Square(100, 0, 60).ExteriorRing;
            Polygon polygon = new(exterior, new[] { hole });
            Circle seed = new(new Vector2(-100, 0), 60);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed, downsample: 1, Options);

            AssertRectangleFits(placement, polygon, seed);
        }

        [TestMethod]
        public void RectangleNormDistance_MeasuresInRectangleUnits()
        {
            // A rectangle 2.15 wide per unit high reaches a wall 4.3 to the right at scale 2, a floor 3 below at scale 3.
            Assert.AreEqual(2, ProposalButtonLayout.RectangleNormDistance(Vector2.Zero, new Vector2(4.3, -10), new Vector2(4.3, 10), 2.15, 1), 1e-9);
            Assert.AreEqual(3, ProposalButtonLayout.RectangleNormDistance(Vector2.Zero, new Vector2(-10, -3), new Vector2(10, -3), 2.15, 1), 1e-9);
            // A diagonal segment is first met at a rectangle corner.
            Assert.AreEqual(1, ProposalButtonLayout.RectangleNormDistance(Vector2.Zero, new Vector2(4.3, 0), new Vector2(0, 2), 2.15, 1), 1e-9);
        }

        [TestMethod]
        public void Fade_RampsFromTheSmallestClickableSize()
        {
            Assert.AreEqual(0, ProposalButtonLayout.FadeFor(radius: 6, downsample: 1, Options), "12 px across: gone");
            Assert.AreEqual(1, ProposalButtonLayout.FadeFor(radius: 7.5, downsample: 1, Options), "15 px across: full");
            Assert.AreEqual(0.5, ProposalButtonLayout.FadeFor(radius: 6.75, downsample: 1, Options), 1e-9, "13.5 px across: half");
            Assert.AreEqual(1, ProposalButtonLayout.FadeFor(radius: 30, downsample: 4, Options), "15 px across at downsample 4");
        }

        [TestMethod]
        public void FadedButtonsTakeNoClicks()
        {
            ProposalButtonPlacement faint = new(new Vector2(0, 0), new Vector2(10, 0), Radius: 4, Fade: 0.4, InsideCircle: false);
            ProposalButtonPlacement clear = faint with { Fade = 0.6 };

            Assert.IsNull(faint.HitTest(new Vector2(0, 0)));
            Assert.AreEqual(ProposalButtonKind.Accept, clear.HitTest(new Vector2(1, 1)));
            Assert.AreEqual(ProposalButtonKind.Hide, clear.HitTest(new Vector2(11, 0)));
            Assert.IsNull(clear.HitTest(new Vector2(5, 0)), "between the buttons");
        }

        [TestMethod]
        public void TinyProposal_FadesOut()
        {
            Polygon polygon = Square(0, 0, 10);
            Circle seed = new(new Vector2(0, 0), 9);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed, downsample: 1, Options);

            Assert.IsNotNull(placement);
            Assert.AreEqual(0, placement.Fade, "a 4 px button is too small to show");
            Assert.IsFalse(placement.IsClickable);
        }

        [TestMethod]
        public void BadDownsample_NoButtons()
        {
            Assert.IsNull(ProposalButtonLayout.Place(Square(0, 0, 10), null, 0, Options));
            Assert.IsNull(ProposalButtonLayout.Place(Square(0, 0, 10), null, double.NaN, Options));
        }
    }
}
