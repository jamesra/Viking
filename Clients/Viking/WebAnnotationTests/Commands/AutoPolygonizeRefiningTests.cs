using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.WPF.Forms;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// A proposal waiting for its overlap-group re-segmentation is marked, so Viking can draw it as provisional.
    /// </summary>
    [TestClass]
    public class AutoPolygonizeRefiningTests
    {
        private static AutoPolygonizeProposal NewProposal() =>
            new(
                controller: null!,
                locationId: 7,
                sectionNumber: 1,
                lastModified: DateTime.UtcNow,
                circleRadius: 10,
                polygon: new Polygon(
                [
                    new Geometry.Vector2(0, 0),
                    new Geometry.Vector2(10, 0),
                    new Geometry.Vector2(10, 10),
                    new Geometry.Vector2(0, 10),
                    new Geometry.Vector2(0, 0)
                ]),
                ringViews: []);

        [TestMethod]
        public void AProposalIsFinalUntilARefinementIsStarted()
        {
            AutoPolygonizeProposal proposal = NewProposal();

            Assert.IsFalse(proposal.IsRefining);
            proposal.BeginRefining();
            Assert.IsTrue(proposal.IsRefining);
            proposal.EndRefining();
            Assert.IsFalse(proposal.IsRefining);
        }

        [TestMethod]
        public void AProposalStaysProvisionalUntilEveryGroupThatClaimedItHasFinished()
        {
            AutoPolygonizeProposal proposal = NewProposal();

            proposal.BeginRefining();
            proposal.BeginRefining();
            proposal.EndRefining();
            Assert.IsTrue(proposal.IsRefining, "a second group is still working on it");
            proposal.EndRefining();
            Assert.IsFalse(proposal.IsRefining);
        }

        [TestMethod]
        public void EndingMoreOftenThanBeginningNeverGoesNegative()
        {
            AutoPolygonizeProposal proposal = NewProposal();

            proposal.EndRefining();
            proposal.EndRefining();
            proposal.BeginRefining();

            Assert.IsTrue(proposal.IsRefining, "an extra End must not cancel the next Begin");
            proposal.EndRefining();
            Assert.IsFalse(proposal.IsRefining);
        }

        [TestMethod]
        public void RefiningIsMarkedAndClearedFromManyThreadsWithoutLosingCount()
        {
            AutoPolygonizeProposal proposal = NewProposal();
            const int perThread = 500;

            System.Threading.Tasks.Parallel.For(0, 8, _ =>
            {
                for (int i = 0; i < perThread; i++)
                    proposal.BeginRefining();
            });
            Assert.IsTrue(proposal.IsRefining);

            System.Threading.Tasks.Parallel.For(0, 8, _ =>
            {
                for (int i = 0; i < perThread; i++)
                    proposal.EndRefining();
            });
            Assert.IsFalse(proposal.IsRefining);
        }

        [TestMethod]
        public void TheShimmerBandEntersBeforeThePolygonAndLeavesAfterIt()
        {
            (float startCenter, float halfWidth) = AutoPolygonizeProposal.ShimmerBand(10, 30, 0);
            (float endCenter, _) = AutoPolygonizeProposal.ShimmerBand(10, 30, 1);

            Assert.IsTrue(halfWidth > 0, "the band has a width");
            Assert.AreEqual(10 - halfWidth, startCenter, 1e-4, "at the start the band ends where the polygon begins");
            Assert.AreEqual(30 + halfWidth, endCenter, 1e-4, "at the end the band starts where the polygon ends");
        }

        [TestMethod]
        public void TheShimmerBandTravelsTheSameNumberOfHalfWidthsWhateverThePolygonSize()
        {
            // The band spans the same share of the polygon's extent whatever the polygon size, so it takes the same
            // number of its own half widths to cross it.
            foreach (float span in new[] { 100f, 1000f })
            {
                (float startCenter, float halfWidth) = AutoPolygonizeProposal.ShimmerBand(0, span, 0);
                (float endCenter, _) = AutoPolygonizeProposal.ShimmerBand(0, span, 1);
                Assert.AreEqual(2 + (1 / 0.15), (endCenter - startCenter) / halfWidth, 1e-3, $"span {span}");
            }
        }

        [TestMethod]
        public void TheShimmerStripeNormalAtMinusQuarterTurnIsTheOriginalDiagonal()
        {
            Microsoft.Xna.Framework.Vector2 normal = AutoPolygonizeProposal.ShimmerStripeNormal(-Math.PI / 4);
            Microsoft.Xna.Framework.Vector2 sweep = AutoPolygonizeProposal.ShimmerSweepDirection(-Math.PI / 4);

            Assert.AreEqual(1f, normal.X, 1e-5);
            Assert.AreEqual(-1f, normal.Y, 1e-5);
            Assert.AreEqual(1f, sweep.X, 1e-5, "the band sweeps along x + y, as it did when the shimmer had stripes");
            Assert.AreEqual(1f, sweep.Y, 1e-5);
        }

        [TestMethod]
        public void TheShimmerSweepIsAQuarterTurnFromTheShaderDirectionAtEveryAngle()
        {
            foreach (double angle in new[] { 0.0, 0.3, 1.2, 2.5, 4.0, 6.0 })
            {
                Microsoft.Xna.Framework.Vector2 normal = AutoPolygonizeProposal.ShimmerStripeNormal(angle);
                Microsoft.Xna.Framework.Vector2 sweep = AutoPolygonizeProposal.ShimmerSweepDirection(angle);

                Assert.AreEqual(0f, Microsoft.Xna.Framework.Vector2.Dot(normal, sweep), 1e-5, $"angle {angle}");
                Assert.AreEqual(Math.Sqrt(2.0), normal.Length(), 1e-5, $"angle {angle}: the band keeps the same scale at every angle");
                Assert.AreEqual(normal.Length(), sweep.Length(), 1e-5, $"angle {angle}");
            }
        }

        [TestMethod]
        public void TheShimmerSweepRangeCoversTheMarkedTexelsAlongTheSweepDirection()
        {
            // A 4 x 3 grid with the texels (1,0), (2,0), (3,2) marked; one clip texel per mask texel.
            bool[] inside = new bool[4 * 3];
            inside[(0 * 4) + 1] = true;
            inside[(0 * 4) + 2] = true;
            inside[(2 * 4) + 3] = true;

            (float low, float high) = AutoPolygonizeProposal.ShimmerSweepRange(inside, 4, 3, 1, new Microsoft.Xna.Framework.Vector2(1, 1));
            Assert.AreEqual(2f, low, 1e-5, "texel (1,0) has centre (1.5, 0.5)");
            Assert.AreEqual(6f, high, 1e-5, "texel (3,2) has centre (3.5, 2.5)");

            (float lowX, float highX) = AutoPolygonizeProposal.ShimmerSweepRange(inside, 4, 3, 1, new Microsoft.Xna.Framework.Vector2(1, -1));
            Assert.AreEqual(1f, lowX, 1e-5, "texel (3,2): 3.5 - 2.5");
            Assert.AreEqual(2f, highX, 1e-5, "texel (2,0): 2.5 - 0.5");

            (float scaledLow, _) = AutoPolygonizeProposal.ShimmerSweepRange(inside, 4, 3, 2, new Microsoft.Xna.Framework.Vector2(1, 1));
            Assert.AreEqual(1f, scaledLow, 1e-5, "positions are in mask texels, so two clip texels per mask texel halve them");
        }

        [TestMethod]
        public void TheShimmerSweepRangeIsZeroWhenNothingIsMarked()
        {
            (float low, float high) = AutoPolygonizeProposal.ShimmerSweepRange(new bool[6], 3, 2, 1, new Microsoft.Xna.Framework.Vector2(1, 1));
            Assert.AreEqual(0f, low);
            Assert.AreEqual(0f, high);
        }

        [TestMethod]
        public void TheShimmerGlowFadesInToItsPeakHalfwayAndOutAgain()
        {
            Assert.AreEqual(0f, AutoPolygonizeProposal.ShimmerGlowAlpha(0.8f, 0), 1e-6);
            Assert.AreEqual(0.8f, AutoPolygonizeProposal.ShimmerGlowAlpha(0.8f, 0.5), 1e-5, "the full shimmer brightness at the midpoint");
            Assert.AreEqual(0f, AutoPolygonizeProposal.ShimmerGlowAlpha(0.8f, 1), 1e-5);
            Assert.IsTrue(AutoPolygonizeProposal.ShimmerGlowAlpha(0.8f, 0.25) < AutoPolygonizeProposal.ShimmerGlowAlpha(0.8f, 0.5));
            Assert.IsTrue(AutoPolygonizeProposal.ShimmerGlowAlpha(0.8f, 0.75) < AutoPolygonizeProposal.ShimmerGlowAlpha(0.8f, 0.5));
            Assert.AreEqual(0f, AutoPolygonizeProposal.ShimmerGlowAlpha(0.8f, 1.7), 1e-5, "progress past the end is clamped");
        }

        [TestMethod]
        public void TheShimmerGlowIsCentredOnTheMarkedTexelsWithAThirdOfTheirLongerSideAsDeviation()
        {
            // Marked texels fill columns 2..5 and rows 1..2 of a 8 x 4 grid, two clip texels per mask texel.
            bool[] inside = new bool[8 * 4];
            for (int y = 1; y <= 2; y++)
                for (int x = 2; x <= 5; x++)
                    inside[(y * 8) + x] = true;

            (Microsoft.Xna.Framework.Vector2 centre, float sigma) = AutoPolygonizeProposal.ShimmerGlowShape(inside, 8, 4, 2);

            Assert.AreEqual(2f, centre.X, 1e-5, "(2 + 5 + 1) / 2 clip texels is 4, which is 2 mask texels");
            Assert.AreEqual(1f, centre.Y, 1e-5, "(1 + 2 + 1) / 2 clip texels is 2, which is 1 mask texel");
            Assert.AreEqual(1f, sigma, 1e-5, "a 4 clip texel long side is 2 mask texels; a third would be 0.67, so the one texel floor applies");
        }

        [TestMethod]
        public void TheShimmerClipIsRasterizedFinerThanTheMask()
        {
            AutoPolygonizeMaskOverlay overlay = new(new byte[8 * 8], 8, 8, new Geometry.Rectangle(new Geometry.Vector2(0, 0), new Geometry.Vector2(32, 32)), 4);
            Polygon square = new(
            [
                new Geometry.Vector2(8, 8),
                new Geometry.Vector2(24, 8),
                new Geometry.Vector2(24, 24),
                new Geometry.Vector2(8, 24),
                new Geometry.Vector2(8, 8)
            ]);

            int supersample = AutoPolygonizeProposal.ShimmerClipSupersample(overlay);
            bool[] coarse = AutoPolygonizeProposal.RasterizePolygon(square, overlay, out _, out _);
            bool[] fine = AutoPolygonizeProposal.RasterizePolygon(square, overlay, out _, out _, supersample);

            Assert.AreEqual(8, supersample, "two clip texels per image pixel, capped at eight per mask texel");
            Assert.AreEqual(coarse.Length * supersample * supersample, fine.Length);
            Assert.AreEqual(coarse.Count(inside => inside) * supersample * supersample, fine.Count(inside => inside),
                "the square covers the same area at both resolutions");
        }

        [TestMethod]
        public void TheRippleStartsAtTheCentreAndItsLastRingLeavesThePolygonByTheEnd()
        {
            const float outerRadius = 40f;
            float halfWidth = AutoPolygonizeProposal.ShimmerRippleHalfWidth(outerRadius);
            float spacing = AutoPolygonizeProposal.ShimmerRippleSpacing(outerRadius);

            float start = AutoPolygonizeProposal.ShimmerRippleRadius(0, outerRadius, spacing, halfWidth);
            float end = AutoPolygonizeProposal.ShimmerRippleRadius(1, outerRadius, spacing, halfWidth);
            float lastRingInnerEdge = end - (2 * spacing) - halfWidth;

            Assert.IsTrue(start <= 0f, "no ring shows when the drop lands");
            Assert.IsTrue(lastRingInnerEdge >= outerRadius - 1e-4f, "the third ring has fully passed the outer radius at the end");
            Assert.AreEqual(end, AutoPolygonizeProposal.ShimmerRippleRadius(1.5, outerRadius, spacing, halfWidth), 1e-5, "progress past the end is clamped");
        }

        [TestMethod]
        public void TheRippleSpreadsMonotonicallyAndSlowsDownLikeWater()
        {
            const float outerRadius = 25f;
            float halfWidth = AutoPolygonizeProposal.ShimmerRippleHalfWidth(outerRadius);
            float spacing = AutoPolygonizeProposal.ShimmerRippleSpacing(outerRadius);

            float previous = AutoPolygonizeProposal.ShimmerRippleRadius(0, outerRadius, spacing, halfWidth);
            for (int step = 1; step <= 20; step++)
            {
                float radius = AutoPolygonizeProposal.ShimmerRippleRadius(step / 20.0, outerRadius, spacing, halfWidth);
                Assert.IsTrue(radius > previous, $"radius grows at step {step}");
                previous = radius;
            }

            float firstHalf = AutoPolygonizeProposal.ShimmerRippleRadius(0.5, outerRadius, spacing, halfWidth);
            float total = AutoPolygonizeProposal.ShimmerRippleRadius(1, outerRadius, spacing, halfWidth);
            Assert.IsTrue(firstHalf > total / 2, "an ease-out covers more than half the distance in the first half");
        }

        [TestMethod]
        public void TheRippleRingsKeepAVisibleWidthOnSmallPolygons()
        {
            Assert.AreEqual(3f, AutoPolygonizeProposal.ShimmerRippleHalfWidth(5f), 1e-6);
            Assert.AreEqual(12f, AutoPolygonizeProposal.ShimmerRippleHalfWidth(100f), 1e-4);
            Assert.AreEqual(28f, AutoPolygonizeProposal.ShimmerRippleSpacing(100f), 1e-4);
        }

        [TestMethod]
        public void TheRippleIsCentredOnTheMarkedTexelsWithHalfTheirDiagonalAsOuterRadius()
        {
            // Marked texels fill columns 2..7 and rows 0..7 of an 8 x 8 grid, two clip texels per mask texel:
            // a 3 x 4 mask texel box centred at (2.5, 2).
            bool[] inside = new bool[8 * 8];
            for (int y = 0; y <= 7; y++)
                for (int x = 2; x <= 7; x++)
                    inside[(y * 8) + x] = true;

            (Microsoft.Xna.Framework.Vector2 centre, float outerRadius) = AutoPolygonizeProposal.ShimmerRippleShape(inside, 8, 8, 2);

            Assert.AreEqual(2.5f, centre.X, 1e-5);
            Assert.AreEqual(2f, centre.Y, 1e-5);
            Assert.AreEqual(2.5f, outerRadius, 1e-5, "half of the 3-4-5 diagonal");
            Assert.AreEqual(1f, AutoPolygonizeProposal.ShimmerRippleShape(new bool[4], 2, 2, 1).OuterRadius, 1e-6, "nothing marked");
        }

        [TestMethod]
        public void TheShimmerStyleChoiceKeepsFixedStylesAndSplitsRandomEvenly()
        {
            foreach (double value in new[] { 0.0, 0.49, 0.5, 0.99 })
            {
                Assert.AreEqual(AutoPolygonizeShimmerStyle.Sweep, AutoPolygonizeProposal.ChooseShimmerStyle(AutoPolygonizeShimmerStyle.Sweep, value));
                Assert.AreEqual(AutoPolygonizeShimmerStyle.Ripple, AutoPolygonizeProposal.ChooseShimmerStyle(AutoPolygonizeShimmerStyle.Ripple, value));
            }

            Assert.AreEqual(AutoPolygonizeShimmerStyle.Sweep, AutoPolygonizeProposal.ChooseShimmerStyle(AutoPolygonizeShimmerStyle.Random, 0.0));
            Assert.AreEqual(AutoPolygonizeShimmerStyle.Sweep, AutoPolygonizeProposal.ChooseShimmerStyle(AutoPolygonizeShimmerStyle.Random, 0.49));
            Assert.AreEqual(AutoPolygonizeShimmerStyle.Ripple, AutoPolygonizeProposal.ChooseShimmerStyle(AutoPolygonizeShimmerStyle.Random, 0.5));
            Assert.AreEqual(AutoPolygonizeShimmerStyle.Ripple, AutoPolygonizeProposal.ChooseShimmerStyle(AutoPolygonizeShimmerStyle.Random, 0.99));
            Assert.AreEqual(AutoPolygonizeShimmerStyle.Sweep, AutoPolygonizeProposal.ChooseShimmerStyle((AutoPolygonizeShimmerStyle)42, 0.9), "unknown values sweep");
        }
    }
}
