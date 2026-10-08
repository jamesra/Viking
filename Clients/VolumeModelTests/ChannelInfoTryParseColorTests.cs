using Geometry.Graphics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="ChannelInfo.TryParseColor"/> used when volume mapping XML supplies channel
    /// <c>Color</c> attributes as either a <see cref="System.Drawing.Color"/> name or an integer/hex literal.
    /// </summary>
    [TestClass]
    public class ChannelInfoTryParseColorTests
    {
        private static void AssertColor(byte r, byte g, byte b, byte a, Color actual)
        {
            Assert.AreEqual(r, actual.R);
            Assert.AreEqual(g, actual.G);
            Assert.AreEqual(b, actual.B);
            Assert.AreEqual(a, actual.A);
        }

        [TestMethod]
        public void TryParseColor_NamedRed_MapsSystemDrawingChannels()
        {
            Assert.IsTrue(ChannelInfo.TryParseColor("Red", out Color output));
            AssertColor(255, 0, 0, 255, output);
        }

        [TestMethod]
        public void TryParseColor_NamedBlue_IsCaseInsensitive()
        {
            Assert.IsTrue(ChannelInfo.TryParseColor("blue", out Color output));
            AssertColor(0, 0, 255, 255, output);
        }

        [TestMethod]
        public void TryParseColor_NamedBlack_UsesNamePathNotIntegerFallback()
        {
            Assert.IsTrue(ChannelInfo.TryParseColor("Black", out Color output));
            AssertColor(0, 0, 0, 255, output);
        }

        [TestMethod]
        public void TryParseColor_UnknownNameWithHexHash_UsesFromInteger()
        {
            Assert.IsTrue(ChannelInfo.TryParseColor("#FF0000", out Color output));
            AssertColor(255, 0, 0, 255, output);
        }

        [TestMethod]
        public void TryParseColor_UnknownNameWith0xPrefix_UsesFromInteger()
        {
            Assert.IsTrue(ChannelInfo.TryParseColor("0x00FF00", out Color output));
            AssertColor(0, 255, 0, 255, output);
        }

        [TestMethod]
        public void TryParseColor_UnknownNameWithDecimalInteger_UsesFromInteger()
        {
            Assert.IsTrue(ChannelInfo.TryParseColor("255", out Color output));
            AssertColor(0, 0, 255, 255, output);
        }

        [TestMethod]
        public void TryParseColor_InvalidIntegerString_ReturnsFalseAndDefaultColor()
        {
            Assert.IsFalse(ChannelInfo.TryParseColor("not-a-color", out Color output));
            AssertColor(0, 0, 0, 0, output);
        }

        [TestMethod]
        public void TryParseColor_EmptyString_ReturnsFalseAndDefaultColor()
        {
            Assert.IsFalse(ChannelInfo.TryParseColor(string.Empty, out Color output));
            AssertColor(0, 0, 0, 0, output);
        }
    }
}
