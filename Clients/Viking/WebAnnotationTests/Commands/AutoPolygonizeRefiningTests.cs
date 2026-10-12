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
        public void TheHatchIsStripesInsideTheMaskAndNothingOutsideIt()
        {
            const int width = 8;
            const int height = 8;
            byte[] bytes = new byte[width * height];
            for (int y = 2; y < 6; y++)
            {
                for (int x = 2; x < 6; x++)
                    bytes[(y * width) + x] = 255;
            }

            AutoPolygonizeMaskOverlay overlay = new(bytes, width, height, new Geometry.Rectangle(new Geometry.Vector2(0, 0), new Geometry.Vector2(32, 32)), 4);
            Color white = Color.White;

            Color[] pixels = AutoPolygonizeProposal.HatchPixels(overlay, white);

            Assert.AreEqual(width * height, pixels.Length);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (bytes[i] == 0)
                    Assert.AreEqual(Color.Transparent, pixels[i], $"outside the mask at {i}");
            }

            Color[] inside = [.. Enumerable.Range(0, pixels.Length).Where(i => bytes[i] != 0).Select(i => pixels[i])];
            Assert.IsTrue(inside.Contains(white), "some inside pixels carry the hatch");
            Assert.IsTrue(inside.Contains(Color.Transparent), "and the stripes between them are clear");
            Assert.AreEqual(white, pixels[(2 * width) + 2]);
            Assert.AreEqual(Color.Transparent, pixels[(2 * width) + 4], "stripes are two texels wide along a row");
        }
    }
}
