using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;

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
    }
}
