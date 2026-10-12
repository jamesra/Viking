using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="SegmentationThinFeatureCut"/>: thin bridges, specks and cut-off lobes go; the edge of anything wide stays put.
    /// </summary>
    [TestClass]
    public class SegmentationThinFeatureCutTests
    {
        private const int W = 60;
        private const int H = 30;

        private static byte[] Field(Func<int, int, bool> inside)
        {
            byte[] soft = new byte[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                    soft[(y * W) + x] = inside(x, y) ? (byte)255 : (byte)0;
            }

            return soft;
        }

        private static bool BlockA(int x, int y) => x >= 3 && x <= 22 && y >= 5 && y <= 24;

        private static bool BlockB(int x, int y) => x >= 33 && x <= 52 && y >= 5 && y <= 24;

        private static bool Bridge(int x, int y) => y == 14 && x >= 23 && x <= 32;

        private static readonly List<(double X, double Y)> ClickInA = [(12.5, 14.5)];

        private static bool IsBackground(byte value) => value < SegmentationMaskPolygonizer.SoftMaskForeground;

        [TestMethod]
        public void AThinBridgeIsCutAndTheLobeItJoinedIsDropped()
        {
            byte[] soft = Field((x, y) => BlockA(x, y) || BlockB(x, y) || Bridge(x, y));

            byte[] cut = SegmentationThinFeatureCut.Cut(soft, W, H, 3, ClickInA, out int cutSamples);

            Assert.IsTrue(cutSamples > 0);
            for (int x = 25; x <= 30; x++)
                Assert.IsTrue(IsBackground(cut[(14 * W) + x]), $"bridge sample {x} cut");
            Assert.IsTrue(IsBackground(cut[(14 * W) + 42]), "the other lobe is dropped with the bridge");
            for (int y = 5; y <= 24; y++)
            {
                for (int x = 3; x <= 22; x++)
                    Assert.AreEqual(255, cut[(y * W) + x], $"the clicked block is untouched at {x},{y}");
            }

            Assert.AreEqual(255, soft[(14 * W) + 28], "the input is not modified");
        }

        [TestMethod]
        public void AWideRegionKeepsItsEdgeExactly()
        {
            byte[] soft = Field(BlockA);

            byte[] cut = SegmentationThinFeatureCut.Cut(soft, W, H, 3, ClickInA, out int cutSamples);

            Assert.AreEqual(0, cutSamples);
            Assert.AreSame(soft, cut, "nothing to cut returns the input itself");
        }

        [TestMethod]
        public void SoftValuesNextToTheEdgeOfAWideRegionAreLeftAlone()
        {
            byte[] soft = Field(BlockA);
            soft[(14 * W) + 23] = 140;
            soft[(14 * W) + 2] = 100;

            byte[] cut = SegmentationThinFeatureCut.Cut(soft, W, H, 3, ClickInA, out _);

            Assert.AreEqual(140, cut[(14 * W) + 23]);
            Assert.AreEqual(100, cut[(14 * W) + 2]);
        }

        [TestMethod]
        public void AnIsolatedSpeckOutsideTheObjectIsRemoved()
        {
            byte[] soft = Field((x, y) => BlockA(x, y) || (x == 40 && y == 3));

            byte[] cut = SegmentationThinFeatureCut.Cut(soft, W, H, 3, ClickInA, out int cutSamples);

            Assert.AreEqual(1, cutSamples);
            Assert.IsTrue(IsBackground(cut[(3 * W) + 40]));
            Assert.AreEqual(255, cut[(14 * W) + 12]);
        }

        [TestMethod]
        public void AWhiskerHangingOffTheEdgeIsCutButTheBodyIsNot()
        {
            byte[] soft = Field((x, y) => BlockA(x, y) || (y == 12 && x >= 23 && x <= 25));

            byte[] cut = SegmentationThinFeatureCut.Cut(soft, W, H, 3, ClickInA, out _);

            Assert.IsTrue(IsBackground(cut[(12 * W) + 25]), "the end of the whisker");
            for (int y = 5; y <= 24; y++)
            {
                for (int x = 3; x <= 22; x++)
                    Assert.AreEqual(255, cut[(y * W) + x]);
            }
        }

        [TestMethod]
        public void WithoutClicksTheLargestPieceIsKept()
        {
            byte[] soft = Field((x, y) => BlockA(x, y) || (x >= 40 && x <= 47 && y >= 10 && y <= 17));

            byte[] cut = SegmentationThinFeatureCut.Cut(soft, W, H, 3, null, out _);

            Assert.AreEqual(255, cut[(14 * W) + 12]);
            Assert.IsTrue(IsBackground(cut[(14 * W) + 43]));
        }

        [TestMethod]
        public void EveryPieceHoldingAClickIsKept()
        {
            byte[] soft = Field((x, y) => BlockA(x, y) || BlockB(x, y));
            List<(double X, double Y)> clicks = [(12.5, 14.5), (42.5, 14.5)];

            byte[] cut = SegmentationThinFeatureCut.Cut(soft, W, H, 3, clicks, out int cutSamples);

            Assert.AreEqual(0, cutSamples);
            Assert.AreSame(soft, cut);
        }

        [TestMethod]
        public void AnObjectThinnerThanTheMinimumIsNeverEmptied()
        {
            byte[] soft = Field((x, y) => x >= 5 && x <= 50 && (y == 14 || y == 15));

            byte[] cut = SegmentationThinFeatureCut.Cut(soft, W, H, 4, [(20.5, 14.5)], out int cutSamples);

            Assert.AreEqual(0, cutSamples);
            Assert.AreSame(soft, cut);
        }

        [TestMethod]
        public void ATinyOrZeroMinimumCutsNothing()
        {
            byte[] soft = Field((x, y) => BlockA(x, y) || Bridge(x, y) || BlockB(x, y));

            Assert.AreSame(soft, SegmentationThinFeatureCut.Cut(soft, W, H, 0, ClickInA, out _));
            Assert.AreSame(soft, SegmentationThinFeatureCut.Cut(soft, W, H, 0.4, ClickInA, out _));
        }

        [TestMethod]
        public void ALargerMinimumCutsABridgeTwoSamplesThick()
        {
            byte[] soft = Field((x, y) => BlockA(x, y) || BlockB(x, y) || ((y == 14 || y == 15) && x >= 23 && x <= 32));

            byte[] keptAtOneAndAHalf = SegmentationThinFeatureCut.Cut(soft, W, H, 1.5, ClickInA, out _);
            byte[] cutAtFive = SegmentationThinFeatureCut.Cut(soft, W, H, 5, ClickInA, out int cutSamples);

            Assert.AreEqual(255, keptAtOneAndAHalf[(14 * W) + 42], "a two-sample bridge survives a 1.5-sample minimum");
            Assert.IsTrue(cutSamples > 0);
            Assert.IsTrue(IsBackground(cutAtFive[(14 * W) + 28]));
            Assert.IsTrue(IsBackground(cutAtFive[(14 * W) + 42]));
        }

        [TestMethod]
        public void TheDistanceTransformMatchesBruteForce()
        {
            Random random = new(7);
            const int w = 23;
            const int h = 17;
            bool[] grid = new bool[w * h];
            for (int i = 0; i < grid.Length; i++)
                grid[i] = random.NextDouble() < 0.12;

            foreach (bool seedValue in new[] { true, false })
            {
                float[] distance = SegmentationThinFeatureCut.SquaredDistanceTo(grid, w, h, seedValue);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        double best = double.MaxValue;
                        for (int sy = 0; sy < h; sy++)
                        {
                            for (int sx = 0; sx < w; sx++)
                            {
                                if (grid[(sy * w) + sx] == seedValue)
                                    best = Math.Min(best, ((x - sx) * (x - sx)) + ((y - sy) * (y - sy)));
                            }
                        }

                        Assert.AreEqual(best, distance[(y * w) + x], 1e-3, $"seed {seedValue} at {x},{y}");
                    }
                }
            }
        }
    }
}
