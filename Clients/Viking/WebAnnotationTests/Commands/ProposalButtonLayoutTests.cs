using System;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="ProposalButtonLayout"/>: the accept and hide buttons sit inside the proposal and outside its circle when
    /// they fit there, inside the circle's lower right otherwise, capped at the largest size, and fade out below the
    /// smallest clickable size.
    /// </summary>
    [TestClass]
    public class ProposalButtonLayoutTests
    {
        private static readonly ProposalButtonOptions Options = new(MaxDiameterPixels: 28, MinDiameterPixels: 12);

        private static Polygon Square(double cx, double cy, double half) => new(new[]
        {
            new Vector2(cx - half, cy - half), new Vector2(cx + half, cy - half), new Vector2(cx + half, cy + half),
            new Vector2(cx - half, cy + half), new Vector2(cx - half, cy - half),
        });

        private static void AssertInsidePolygonAndOutsideSeed(ProposalButtonPlacement placement, Polygon polygon, Circle seed)
        {
            foreach (Vector2 center in new[] { placement.Accept, placement.Hide })
            {
                Assert.IsTrue(polygon.Contains(center), $"button at {center} is inside the proposal");
                Assert.IsTrue(Vector2.Distance(center, seed.Center) >= seed.Radius + placement.Radius,
                    $"button at {center} is clear of the circle");
            }
        }

        [TestMethod]
        public void RoomOutsideTheCircle_ButtonsGoThere()
        {
            Polygon polygon = Square(0, 0, 200);
            Circle seed = new(new Vector2(-100, 0), 60);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed, downsample: 1, Options);

            Assert.IsNotNull(placement);
            Assert.IsFalse(placement.InsideCircle);
            AssertInsidePolygonAndOutsideSeed(placement, polygon, seed);
            Assert.AreEqual(14, placement.Radius, 1e-9, "capped at half the largest size");
            Assert.AreEqual(1, placement.Fade);
            Assert.IsTrue(Vector2.Distance(placement.Accept, placement.Hide) >= 2 * placement.Radius, "the buttons do not overlap");
            Assert.IsTrue(Vector2.Distance(placement.Accept, placement.Hide) <= placement.Radius * ProposalButtonLayout.MaxPairSpacingRadii + 1,
                "the buttons stay together");
        }

        [TestMethod]
        public void SizeFollowsTheZoom()
        {
            Polygon polygon = Square(0, 0, 2000);
            Circle seed = new(new Vector2(-1000, 0), 600);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed, downsample: 4, Options);

            Assert.AreEqual(14 * 4, placement.Radius, 1e-9, "28 screen pixels across at downsample 4");
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
            Assert.IsTrue(polygon.Contains(placement.Accept) && polygon.Contains(placement.Hide));
        }

        [TestMethod]
        public void PolygonWithAHole_ButtonsAvoidTheHole()
        {
            Vector2[] exterior = Square(0, 0, 200).ExteriorRing;
            Vector2[] hole = Square(100, 0, 60).ExteriorRing;
            Polygon polygon = new(exterior, new[] { hole });
            Circle seed = new(new Vector2(-100, 0), 60);

            ProposalButtonPlacement placement = ProposalButtonLayout.Place(polygon, seed, downsample: 1, Options);

            AssertInsidePolygonAndOutsideSeed(placement, polygon, seed);
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
