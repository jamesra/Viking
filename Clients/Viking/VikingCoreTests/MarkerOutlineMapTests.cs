using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using VikingXNAGraphics;

namespace VikingCoreTests
{
    /// <summary>
    /// The picture an icon marker is drawn from with a contrasting border: the icon shrunk about the centre so its outer ring has
    /// room for the border, its coverage in R, its luma mask in A and its signed distance field in G.
    /// </summary>
    [TestClass]
    public class MarkerOutlineMapTests
    {
        private const int Size = 100;

        /// <summary>Pixels G spans either side of the edge in a <see cref="Size"/> picture.</summary>
        private const float Range = Size * MarkerOutlineMap.DistanceRange;

        /// <summary>An icon whose coverage is a ring reaching the edge of the picture, with a mask disc as large as the picture.</summary>
        private static byte[] RingIcon()
        {
            byte[] rgba = new byte[Size * Size * 4];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    double r = Math.Sqrt(Math.Pow(x + 0.5 - Size / 2.0, 2) + Math.Pow(y + 0.5 - Size / 2.0, 2));
                    byte ring = r >= 44 && r <= 49 ? (byte)255 : (byte)0;
                    int i = (y * Size + x) * 4;
                    rgba[i] = rgba[i + 1] = rgba[i + 2] = ring;
                    rgba[i + 3] = r <= 50 ? (byte)255 : (byte)0;
                }
            }

            return rgba;
        }

        /// <summary>An antialiased disc of <paramref name="radius"/> pixels about the centre, its coverage in R and a full mask in A.</summary>
        private static byte[] DiscIcon(double radius)
        {
            byte[] rgba = new byte[Size * Size * 4];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    double r = Math.Sqrt(Math.Pow(x + 0.5 - Size / 2.0, 2) + Math.Pow(y + 0.5 - Size / 2.0, 2));
                    int i = (y * Size + x) * 4;
                    rgba[i] = (byte)Math.Round(255 * Math.Min(Math.Max(radius - r + 0.5, 0), 1));
                    rgba[i + 3] = 255;
                }
            }

            return rgba;
        }

        /// <summary>The distance from the edge, in pixels and negative inside, that a pixel's G stands for.</summary>
        private static double EdgeDistance(byte[] packed, int x, int y) => (0.5 - Channel(packed, x, y, 1) / 255.0) * 2 * Range;

        private static byte Channel(byte[] packed, int x, int y, int channel) => packed[(y * Size + x) * 4 + channel];

        [TestMethod]
        public void TheIconShrinksAboutTheCentreLeavingRoomForTheBorder()
        {
            byte[] map = MarkerOutlineMap.Build(RingIcon(), Size, Size, 0.8f);

            // The ring at radius 44-49 now sits at 35-39; the source ring's own radius is outside it
            Assert.AreEqual(255, Channel(map, 50 + 37, 50, 0), "on the shrunk ring");
            Assert.AreEqual(0, Channel(map, 50 + 46, 50, 0), "where the ring was");
            Assert.AreEqual(0, Channel(map, 50, 50, 0), "the centre stays empty");
        }

        [TestMethod]
        public void GreenIsHalfOnTheEdgeAboveItInsideAndFallsToNothingAtTheRange()
        {
            byte[] map = MarkerOutlineMap.Build(RingIcon(), Size, Size, 0.8f);

            Assert.IsTrue(Channel(map, 50 + 37, 50, 1) > 128, "inside the shrunk ring");
            byte near = Channel(map, 50 + 40, 50, 1);
            byte far = Channel(map, 50 + 43, 50, 1);
            Assert.IsTrue(128 > near && near > far && far > 0, $"the field falls outward ({near}, {far})");
            Assert.AreEqual(0, Channel(map, 50 + 47, 50, 1), "beyond the range");
        }

        [TestMethod]
        public void TheGapsInsideTheIconAreEmpty()
        {
            byte[] map = MarkerOutlineMap.Build(RingIcon(), Size, Size, 0.8f);

            // The middle of the ring is further from it than the range, so the shader draws neither icon nor border there
            Assert.AreEqual(0, Channel(map, 50, 50, 1));
        }

        [TestMethod]
        public void TheEdgeIsFoundBetweenPixelsSoItStaysRoundWhenMagnified()
        {
            const double Radius = 30.3;
            byte[] map = MarkerOutlineMap.Build(DiscIcon(Radius), Size, Size, 1f);

            // Every pixel near the edge reads its own distance from the true circle; measuring on whole pixels would be up to half a pixel out
            double worst = 0;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    double r = Math.Sqrt(Math.Pow(x + 0.5 - Size / 2.0, 2) + Math.Pow(y + 0.5 - Size / 2.0, 2));
                    if (Math.Abs(r - Radius) <= 3)
                        worst = Math.Max(worst, Math.Abs(EdgeDistance(map, x, y) - (r - Radius)));
                }
            }

            Assert.IsTrue(worst < 0.35, $"worst error {worst:F2} pixels");
        }

        [TestMethod]
        public void TheMaskShrinksWithTheIconAndBlueRepeatsTheCoverage()
        {
            byte[] map = MarkerOutlineMap.Build(RingIcon(), Size, Size, 0.8f);

            Assert.AreEqual(255, Channel(map, 50, 50, 3), "inside the mask");
            Assert.AreEqual(0, Channel(map, 50 + 45, 50, 3), "the mask disc is shrunk to radius 40");
            Assert.AreEqual(Channel(map, 50 + 37, 50, 0), Channel(map, 50 + 37, 50, 2));
        }

        [TestMethod]
        public void ABadSizeOrScaleIsRefused()
        {
            Assert.ThrowsException<ArgumentException>(() => MarkerOutlineMap.Build(new byte[10], Size, Size));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MarkerOutlineMap.Build(RingIcon(), Size, Size, 0f));
        }
    }
}
