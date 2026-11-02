using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using VikingXNAGraphics;

namespace VikingCoreTests
{
    /// <summary>
    /// The picture an icon marker is drawn from with a contrasting border: the icon shrunk about the centre so its outer ring has
    /// room for the border, its coverage in R, its luma mask in A and the distance ramp from it in G.
    /// </summary>
    [TestClass]
    public class MarkerOutlineMapTests
    {
        private const int Size = 100;
        private const int Range = 8;

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

        private static byte Channel(byte[] packed, int x, int y, int channel) => packed[(y * Size + x) * 4 + channel];

        [TestMethod]
        public void TheIconShrinksAboutTheCentreLeavingRoomForTheBorder()
        {
            byte[] map = MarkerOutlineMap.Build(RingIcon(), Size, Size, 0.8f, Range);

            // The ring at radius 44-49 now sits at 35-39; the source ring's own radius is outside it
            Assert.AreEqual(255, Channel(map, 50 + 37, 50, 0), "on the shrunk ring");
            Assert.AreEqual(0, Channel(map, 50 + 46, 50, 0), "where the ring was");
            Assert.AreEqual(0, Channel(map, 50, 50, 0), "the centre stays empty");
        }

        [TestMethod]
        public void GreenIsFullOnTheIconAndFallsToNothingAtTheRange()
        {
            byte[] map = MarkerOutlineMap.Build(RingIcon(), Size, Size, 0.8f, Range);

            Assert.AreEqual(255, Channel(map, 50 + 37, 50, 1));
            byte near = Channel(map, 50 + 42, 50, 1);
            byte far = Channel(map, 50 + 45, 50, 1);
            Assert.IsTrue(near > far && far > 0, $"the ramp falls outward ({near}, {far})");
            Assert.AreEqual(0, Channel(map, 99, 50, 1), "beyond the range");
        }

        [TestMethod]
        public void TheMaskShrinksWithTheIconAndBlueRepeatsTheCoverage()
        {
            byte[] map = MarkerOutlineMap.Build(RingIcon(), Size, Size, 0.8f, Range);

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
