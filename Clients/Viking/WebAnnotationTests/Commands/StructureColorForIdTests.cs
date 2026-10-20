using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The cell hue is a hash of the structure id. Segmentation and autoseg read the same value.
    /// <see cref="StructureObj.RandomColor"/> re-rolls a color from saturation 0.75 and value 0.95, so channels
    /// span round(0.2375 * 255) = 61 to round(0.95 * 255) = 242.
    /// </summary>
    [TestClass]
    public class StructureColorForIdTests
    {
        [TestMethod]
        public void ColorForId_SameIdIsStableAndDifferentIdsDiffer()
        {
            uint first = StructureObj.ColorForId(42);
            uint again = StructureObj.ColorForId(42);
            uint other = StructureObj.ColorForId(43);

            Assert.AreEqual(first, again);
            Assert.AreNotEqual(first, other);
            Assert.AreEqual(0xFFu, (first >> 24) & 0xFFu);
        }

        /// <summary>Random whose <see cref="NextDouble"/> returns a fixed value, so a hue sector can be chosen exactly.</summary>
        private sealed class FixedRandom : Random
        {
            private readonly double _value;

            public FixedRandom(double value) => _value = value;

            protected override double Sample() => _value;

            public override double NextDouble() => _value;
        }

        private const uint Low = 0x3D;
        private const uint High = 0xF2;

        private static uint Pack(uint r, uint g, uint b) => 0xFF000000u | (r << 16) | (g << 8) | b;

        [TestMethod]
        public void RandomColor_NullRandomThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(() => StructureObj.RandomColor(null!));
        }

        [TestMethod]
        public void RandomColor_AlphaIsOpaqueAndChannelsStayWithinSaturationAndValueBounds()
        {
            var random = new Random(12345);
            for (int i = 0; i < 2000; i++)
            {
                uint color = StructureObj.RandomColor(random);
                Assert.AreEqual(0xFFu, color >> 24);
                uint[] channels = { (color >> 16) & 0xFFu, (color >> 8) & 0xFFu, color & 0xFFu };
                foreach (uint channel in channels)
                {
                    Assert.IsTrue(channel >= Low && channel <= High, $"channel {channel} outside {Low}..{High}");
                }
                Assert.AreEqual(High, Math.Max(channels[0], Math.Max(channels[1], channels[2])) , "brightest channel is value * 255");
            }
        }

        [TestMethod]
        public void RandomColor_SameSeedGivesSameColor()
        {
            Assert.AreEqual(StructureObj.RandomColor(new Random(7)), StructureObj.RandomColor(new Random(7)));
        }

        [TestMethod]
        public void RandomColor_SectorStartsPackExactArgb()
        {
            // At the start of each sector the fraction is 0: one channel at value, one at the floor, one rising/falling from them.
            Assert.AreEqual(Pack(High, Low, Low), StructureObj.RandomColor(new FixedRandom(0.0)));
            Assert.AreEqual(Pack(High, High, Low), StructureObj.RandomColor(new FixedRandom(1.0 / 6.0)));
            Assert.AreEqual(Pack(Low, High, Low), StructureObj.RandomColor(new FixedRandom(2.0 / 6.0)));
            Assert.AreEqual(Pack(Low, High, High), StructureObj.RandomColor(new FixedRandom(3.0 / 6.0)));
            Assert.AreEqual(Pack(Low, Low, High), StructureObj.RandomColor(new FixedRandom(4.0 / 6.0)));
            Assert.AreEqual(Pack(High, Low, High), StructureObj.RandomColor(new FixedRandom(5.0 / 6.0)));
        }

        [TestMethod]
        public void RandomColor_SectorMidpointsPackExactArgb()
        {
            // Fraction 0.5: the moving channel is round(0.95 * (1 - 0.75 * 0.5) * 255) = 151 (0x97).
            const uint mid = 0x97;
            Assert.AreEqual(Pack(High, mid, Low), StructureObj.RandomColor(new FixedRandom(0.5 / 6.0)));
            Assert.AreEqual(Pack(mid, High, Low), StructureObj.RandomColor(new FixedRandom(1.5 / 6.0)));
            Assert.AreEqual(Pack(Low, High, mid), StructureObj.RandomColor(new FixedRandom(2.5 / 6.0)));
            Assert.AreEqual(Pack(Low, mid, High), StructureObj.RandomColor(new FixedRandom(3.5 / 6.0)));
            Assert.AreEqual(Pack(mid, Low, High), StructureObj.RandomColor(new FixedRandom(4.5 / 6.0)));
            Assert.AreEqual(Pack(High, Low, mid), StructureObj.RandomColor(new FixedRandom(5.5 / 6.0)));
        }

        [TestMethod]
        public void RandomColor_NextDoubleJustBelowOneStaysInLastSector()
        {
            // 0.9999999 * 6 is just under 6, so the index must stay 5 (no wrap to sector 0 or out-of-range) and the fraction is near 1.
            uint color = StructureObj.RandomColor(new FixedRandom(0.9999999));
            Assert.AreEqual(Pack(High, Low, Low), color);
        }
    }
}
