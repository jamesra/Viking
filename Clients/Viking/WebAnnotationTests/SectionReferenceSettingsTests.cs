using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Models;
using Viking.VolumeModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the channel color round trip of <see cref="SectionReferenceSettings"/>: a channel saved to
    /// section-references.json must load back with the same color, including alpha, which the section viewer
    /// uses as the channel's blend weight.
    /// </summary>
    [TestClass]
    public class SectionReferenceSettingsTests
    {
        private static ChannelInfo Channel(byte a, byte r, byte g, byte b) => new()
        {
            ChannelName = "ch",
            SectionSource = ChannelInfo.SectionInfo.FIXED,
            FixedSectionNumber = 7,
            Color = new Geometry.Graphics.Color(r, g, b, a),
        };

        [TestMethod]
        public void ToDto_WritesAlphaFirstHex()
        {
            Assert.AreEqual("#FF123456", SectionReferenceSettings.ToDto(Channel(255, 0x12, 0x34, 0x56)).ColorHex);
        }

        [TestMethod]
        [DataRow((byte)255)]
        [DataRow((byte)128)]
        [DataRow((byte)127)]
        [DataRow((byte)1)]
        [DataRow((byte)0)]
        public void RoundTrip_KeepsColorAndAlpha(byte alpha)
        {
            ChannelInfo restored = SectionReferenceSettings.FromDto(SectionReferenceSettings.ToDto(Channel(alpha, 0x12, 0x34, 0x56)));

            Assert.AreEqual(alpha, restored.Color.A);
            Assert.AreEqual(0x12, restored.Color.R);
            Assert.AreEqual(0x34, restored.Color.G);
            Assert.AreEqual(0x56, restored.Color.B);
            Assert.AreEqual("ch", restored.ChannelName);
            Assert.AreEqual(ChannelInfo.SectionInfo.FIXED, restored.SectionSource);
            Assert.AreEqual(7, restored.FixedSectionNumber);
        }

        [TestMethod]
        public void FromDto_BlankOrBadColor_FallsBackToOpaqueWhite()
        {
            foreach (string hex in new[] { null, "  ", "#ZZZZZZZZ" })
            {
                Geometry.Graphics.Color color = SectionReferenceSettings.FromDto(new ChannelInfoDto { ColorHex = hex }).Color;
                Assert.AreEqual((byte)255, color.A);
                Assert.AreEqual((byte)255, color.R);
                Assert.AreEqual((byte)255, color.G);
                Assert.AreEqual((byte)255, color.B);
            }
        }
    }
}
