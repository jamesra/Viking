using Microsoft.VisualStudio.TestTools.UnitTesting;
using VikingXNAGraphics;

namespace VikingCoreTests
{
    /// <summary>The picture a label is cached as for the outline shader: coverage in R, a linear fall-off with distance from the text in G.</summary>
    [TestClass]
    public class LabelOutlineMapTests
    {
        private const int Size = 41;
        private const int Range = 10;

        /// <summary>A 5 x 5 block of text in the middle of an otherwise blank picture.</summary>
        private static byte[] Block()
        {
            byte[] coverage = new byte[Size * Size];
            for (int y = 18; y < 23; y++)
            {
                for (int x = 18; x < 23; x++)
                    coverage[y * Size + x] = 255;
            }

            return coverage;
        }

        private static byte Channel(byte[] packed, int x, int y, int channel) => packed[(y * Size + x) * 4 + channel];

        [TestMethod]
        public void CoverageIsKeptAndTheGreenChannelIsFullOnTheText()
        {
            byte[] packed = LabelOutlineMap.Build(Block(), Size, Size, Range);

            Assert.AreEqual(255, Channel(packed, 20, 20, 0));
            Assert.AreEqual(255, Channel(packed, 20, 20, 1));
            Assert.AreEqual(0, Channel(packed, 2, 2, 0));
            Assert.AreEqual(255, Channel(packed, 2, 2, 3), "opaque, so mip generation keeps the channels");
        }

        [TestMethod]
        public void TheGreenChannelFallsLinearlyWithDistanceFromTheEdgeAndStopsAtTheRange()
        {
            byte[] packed = LabelOutlineMap.Build(Block(), Size, Size, Range);

            // Text covers x 18..22. Pixel centre 23 is 1 pixel from the nearest text centre, so 0.5 from the edge.
            double expectedAtOne = 255 * (1 - 0.5 / Range);
            Assert.AreEqual(expectedAtOne, Channel(packed, 23, 20, 1), 1.0);

            // Each further pixel to the right takes 1/Range of the ramp.
            double step = Channel(packed, 24, 20, 1) - Channel(packed, 25, 20, 1);
            Assert.AreEqual(255.0 / Range, step, 1.0);

            Assert.AreEqual(0, Channel(packed, 35, 20, 1), "further than the range is zero");
        }

        [TestMethod]
        public void DistanceIsEuclideanSoCornersFallFasterThanSides()
        {
            float[] distance = LabelOutlineMap.DistanceToInside(Block(), Size, Size);

            Assert.AreEqual(0f, distance[20 * Size + 20]);
            Assert.AreEqual(3f, distance[20 * Size + 25], 1e-4);
            Assert.AreEqual(3f * (float)System.Math.Sqrt(2), distance[25 * Size + 25], 1e-4);
        }

        [TestMethod]
        public void AnEmptyPictureIsFarEverywhere()
        {
            byte[] packed = LabelOutlineMap.Build(new byte[Size * Size], Size, Size, Range);

            for (int i = 0; i < Size * Size; i++)
                Assert.AreEqual(0, packed[i * 4 + 1]);
        }

        [TestMethod]
        public void AWrongSizedPictureIsRefused()
        {
            Assert.ThrowsException<System.ArgumentException>(() => LabelOutlineMap.Build(new byte[10], Size, Size, Range));
        }
    }
}
