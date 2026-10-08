using System.Linq;
using System.Xml.Linq;
using FsCheck;
using Geometry.Graphics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="ChannelInfo.FromXML"/> when volume mapping XML lists default or per-section
    /// <c>Channel</c> elements (section source, channel name, color, greyscale blending hint).
    /// Real volume XML always supplies <c>Section</c>, <c>Channel</c>, and <c>Color</c>; missing
    /// attributes throw <see cref="XMLMissingDataException"/> via <see cref="IO.GetAttributeCaseInsensitive"/>.
    /// </summary>
    [TestClass]
    public class ChannelInfoFromXmlTests
    {
        private static XElement ChannelInfoRoot(params string[] channelElements) =>
            new XElement("ChannelInfo", channelElements.Select(XElement.Parse));

        private static string ChannelElement(string section, string channel, string color) =>
            $"<Channel Section=\"{section}\" Channel=\"{channel}\" Color=\"{color}\" />";

        [TestMethod]
        public void FromXML_Null_ReturnsEmpty()
        {
            CollectionAssert.AreEqual(System.Array.Empty<ChannelInfo>(), ChannelInfo.FromXML(null));
        }

        [TestMethod]
        public void FromXML_MissingChannelAttribute_ThrowsXmlMissingDataException()
        {
            var ex = Assert.ThrowsException<XMLMissingDataException>(() =>
                ChannelInfo.FromXML(ChannelInfoRoot("<Channel Section=\"Selected\" Color=\"#FF0000\" />")));
            StringAssert.Contains(ex.Message, "Channel");
        }

        [TestMethod]
        public void FromXML_SectionSelected_MapsSectionSource()
        {
            ChannelInfo[] channels = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("Selected", "Selected", "#FF0000")));

            Assert.AreEqual(1, channels.Length);
            Assert.AreEqual(ChannelInfo.SectionInfo.SELECTED, channels[0].SectionSource);
            Assert.IsFalse(channels[0].FixedSectionNumber.HasValue);
        }

        [TestMethod]
        public void FromXML_SectionAboveAndBelow_AreCaseInsensitive()
        {
            ChannelInfo[] above = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("above", "Selected", "#00FF00")));
            ChannelInfo[] below = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("BELOW", "Selected", "#0000FF")));

            Assert.AreEqual(ChannelInfo.SectionInfo.ABOVE, above[0].SectionSource);
            Assert.AreEqual(ChannelInfo.SectionInfo.BELOW, below[0].SectionSource);
        }

        [TestMethod]
        public void FromXML_SectionNumericFixed_ParsesFixedSectionNumber()
        {
            ChannelInfo[] channels = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("42", "Selected", "#FFFFFF")));

            Assert.AreEqual(1, channels.Length);
            Assert.AreEqual(ChannelInfo.SectionInfo.FIXED, channels[0].SectionSource);
            Assert.AreEqual(42, channels[0].FixedSectionNumber);
        }

        [TestMethod]
        public void FromXML_ChannelSelected_ClearsChannelName()
        {
            ChannelInfo[] channels = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("Selected", "Selected", "#FFFFFF")));

            Assert.AreEqual(1, channels.Length);
            Assert.AreEqual(string.Empty, channels[0].ChannelName);
        }

        [TestMethod]
        public void FromXML_NamedChannel_PreservesChannelName()
        {
            ChannelInfo[] channels = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("Selected", "GFP", "#FFFFFF")));

            Assert.AreEqual("GFP", channels[0].ChannelName);
        }

        [TestMethod]
        public void FromXML_InvalidSection_SkipsChannel()
        {
            ChannelInfo[] channels = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("not-a-number", "Selected", "#FFFFFF")));

            Assert.AreEqual(0, channels.Length);
        }

        [TestMethod]
        public void FromXML_InvalidColor_SkipsChannel()
        {
            ChannelInfo[] channels = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("Selected", "Selected", "not-a-color")));

            Assert.AreEqual(0, channels.Length);
        }

        [TestMethod]
        public void FromXML_GreyscaleWhenRgbEqual_SetsGreyscaleProperty()
        {
            ChannelInfo[] channels = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("Selected", "Selected", "#808080")));

            Assert.AreEqual(1, channels.Length);
            Assert.IsTrue(channels[0].Greyscale);
            Assert.AreEqual((byte)0x80, channels[0].Color.R);
            Assert.AreEqual((byte)0x80, channels[0].Color.G);
            Assert.AreEqual((byte)0x80, channels[0].Color.B);
        }

        [TestMethod]
        public void FromXML_NonGreyscaleColor_GreyscaleIsFalse()
        {
            ChannelInfo[] channels = ChannelInfo.FromXML(
                ChannelInfoRoot(ChannelElement("Selected", "Selected", "#FF0000")));

            Assert.IsFalse(channels[0].Greyscale);
        }

        [TestMethod]
        public void FromXML_ValidChannelElements_MatchReferenceParse()
        {
            var sectionKeywordGen = Gen.Elements("selected", "above", "below", "Selected", "ABOVE");
            var hexColorGen = Gen.Choose(0, 0xFFFFFF).Select(v => $"#{v:X6}");
            var optionalChannelGen = Gen.Elements("GFP", "DAPI", "Selected");

            var prop = Prop.ForAll(
                sectionKeywordGen.ToArbitrary(),
                hexColorGen.ToArbitrary(),
                optionalChannelGen.ToArbitrary(),
                (section, color, channelAttr) =>
                {
                    string channelXml = ChannelElement(section, channelAttr, color);

                    ChannelInfo[] parsed = ChannelInfo.FromXML(ChannelInfoRoot(channelXml));
                    if (parsed.Length != 1)
                        return false;

                    ChannelInfo channel = parsed[0];
                    string sectionLower = section.ToLowerInvariant();
                    bool expectFixed = sectionLower is not ("selected" or "above" or "below");
                    if (expectFixed)
                        return false;

                    if (channel.SectionSource != SectionSourceFromKeyword(sectionLower))
                        return false;

                    if (channel.FixedSectionNumber.HasValue)
                        return false;

                    string expectedName = channelAttr == "Selected" ? string.Empty : channelAttr;
                    if (channel.ChannelName != expectedName)
                        return false;

                    if (!ChannelInfo.TryParseColor(color, out Color expectedColor))
                        return false;

                    return channel.Color.R == expectedColor.R
                           && channel.Color.G == expectedColor.G
                           && channel.Color.B == expectedColor.B;
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }

        [TestMethod]
        public void FromXML_FixedSectionNumbers_RoundTripNumericSection()
        {
            var prop = Prop.ForAll(
                Arb.Default.PositiveInt().Generator.Where(n => n.Get <= 10_000).ToArbitrary(),
                Arb.From(Gen.Elements("#FFFFFF", "#FF0000", "#00FF00", "#0000FF")),
                (section, color) =>
                {
                    string xml = ChannelElement(section.Get.ToString(), "Selected", color);
                    ChannelInfo[] parsed = ChannelInfo.FromXML(ChannelInfoRoot(xml));
                    if (parsed.Length != 1)
                        return false;

                    ChannelInfo channel = parsed[0];
                    return channel.SectionSource == ChannelInfo.SectionInfo.FIXED
                           && channel.FixedSectionNumber == section.Get;
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }

        private static ChannelInfo.SectionInfo SectionSourceFromKeyword(string sectionLower) =>
            sectionLower switch
            {
                "selected" => ChannelInfo.SectionInfo.SELECTED,
                "above" => ChannelInfo.SectionInfo.ABOVE,
                "below" => ChannelInfo.SectionInfo.BELOW,
                _ => ChannelInfo.SectionInfo.FIXED
            };
    }
}
