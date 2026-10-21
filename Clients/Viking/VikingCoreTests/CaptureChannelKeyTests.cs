using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.Controls;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="SectionViewerControl.CaptureChannelKeyFor"/>, the channel part of the segmentation server's shared tile
    /// key: views that capture different pictures must get different keys, and a plain greyscale view keeps the bare channel
    /// name so its tiles are shared with clients that send only that.
    /// </summary>
    [TestClass]
    public class CaptureChannelKeyTests
    {
        private static ChannelInfo Channel(string name, byte r, byte g, byte b) =>
            new() { ChannelName = name, Color = new Geometry.Graphics.Color(r, g, b, (byte)255) };

        [TestMethod]
        public void APlainGreyscaleViewIsTheChannelName()
        {
            Assert.AreEqual("TEM", SectionViewerControl.CaptureChannelKeyFor("TEM", [Channel("", 255, 255, 255)], false, false, false));
            Assert.AreEqual("TEM", SectionViewerControl.CaptureChannelKeyFor("TEM", [], false, false, false));
        }

        [TestMethod]
        public void ABlendDiffersFromTheSingleChannelItIsBuiltOn()
        {
            string single = SectionViewerControl.CaptureChannelKeyFor("TEM", [Channel("", 255, 255, 255)], false, false, false);
            string blend = SectionViewerControl.CaptureChannelKeyFor(
                "TEM", [Channel("", 255, 255, 255), Channel("GABA", 255, 0, 0)], false, false, false);

            Assert.AreNotEqual(single, blend);
        }

        [TestMethod]
        public void BlendsDifferingInOneChannelColourDiffer()
        {
            string red = SectionViewerControl.CaptureChannelKeyFor(
                "TEM", [Channel("", 255, 255, 255), Channel("GABA", 255, 0, 0)], false, false, false);
            string green = SectionViewerControl.CaptureChannelKeyFor(
                "TEM", [Channel("", 255, 255, 255), Channel("GABA", 0, 255, 0)], false, false, false);

            Assert.AreNotEqual(red, green);
        }

        [TestMethod]
        public void ASingleColouredChannelDiffersFromItsGreyscaleView()
        {
            Assert.AreNotEqual(
                SectionViewerControl.CaptureChannelKeyFor("TEM", [Channel("", 255, 255, 255)], false, false, false),
                SectionViewerControl.CaptureChannelKeyFor("TEM", [Channel("", 255, 128, 0)], false, false, false));
        }

        [TestMethod]
        public void ViewOptionsDrawnIntoACaptureChangeTheKey()
        {
            ChannelInfo[] grey = [Channel("", 255, 255, 255)];
            string plain = SectionViewerControl.CaptureChannelKeyFor("TEM", grey, false, false, false);

            Assert.AreNotEqual(plain, SectionViewerControl.CaptureChannelKeyFor("TEM", grey, true, false, false));
            Assert.AreNotEqual(plain, SectionViewerControl.CaptureChannelKeyFor("TEM", grey, false, true, false));
            Assert.AreNotEqual(plain, SectionViewerControl.CaptureChannelKeyFor("TEM", grey, false, false, true));
        }
    }
}
