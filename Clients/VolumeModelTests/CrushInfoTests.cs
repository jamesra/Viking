using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// <see cref="CrushInfo"/> reads the Crush* attributes Nornir writes on a crushed filter's Pyramid and Tileset elements.
    /// </summary>
    [TestClass]
    public class CrushInfoTests
    {
        private const string Crushed =
            "<Tileset path=\"t\" CrushVersion=\"1\" CrushStep=\"120\" CrushOffset=\"0\" CrushBits=\"8\" CrushDisplayMin=\"0\" CrushDisplayMax=\"46\">" +
            "<CodeHistogram Bins=\"4\">5 10 20 5</CodeHistogram></Tileset>";

        [TestMethod]
        public void TheCrushAttributesAndHistogramAreRead()
        {
            CrushInfo? crush = CrushInfo.FromElement(XElement.Parse(Crushed));

            Assert.IsNotNull(crush);
            Assert.AreEqual(120, crush.Step);
            Assert.AreEqual(0, crush.Offset);
            Assert.AreEqual(8, crush.Bits);
            Assert.AreEqual((0.0, 46.0), crush.DisplayRange);
            CollectionAssert.AreEqual(new long[] { 5, 10, 20, 5 }, (System.Collections.ICollection)crush.CodeHistogram!);
            Assert.IsTrue(crush.IsSupported);
        }

        [TestMethod]
        public void AnElementWithoutACrushVersionIsNotCrushed()
        {
            Assert.IsNull(CrushInfo.FromElement(XElement.Parse("<Tileset path=\"t\" CrushStep=\"120\" CrushBits=\"8\" />")));
        }

        [TestMethod]
        public void AnElementWithAnUnusableStepIsNotCrushed()
        {
            Assert.IsNull(CrushInfo.FromElement(XElement.Parse("<Pyramid CrushVersion=\"1\" CrushStep=\"0\" CrushBits=\"8\" />")));
            Assert.IsNull(CrushInfo.FromElement(XElement.Parse("<Pyramid CrushVersion=\"1\" CrushBits=\"8\" />")));
        }

        [TestMethod]
        public void TheDisplayRangeBecomesAWindowOfPngSamples()
        {
            (float min, float max) = CrushInfo.FromElement(XElement.Parse(Crushed))!.DefaultWindow();

            Assert.AreEqual(0f, min);
            Assert.AreEqual(46f / 255f, max, 1e-6);
        }

        [TestMethod]
        public void WithoutADisplayRangeTheWindowComesFromTheHistogramSkippingTheEmptyBin()
        {
            // Bin 0 holds the empty section edge and must not set the black point.
            XElement element = XElement.Parse(
                "<Pyramid CrushVersion=\"1\" CrushStep=\"10\" CrushBits=\"8\"><CodeHistogram>1000000 0 0 100 0 0 0 100 0 0</CodeHistogram></Pyramid>");

            (float min, float max) = CrushInfo.FromElement(element)!.DefaultWindow();

            Assert.AreEqual(3f / 255f, min, 1e-6);
            Assert.AreEqual(7f / 255f, max, 1e-6);
        }

        [TestMethod]
        public void WithNeitherRangeNorHistogramTheWindowCoversEveryCode()
        {
            (float min, float max) = CrushInfo.FromElement(XElement.Parse("<Pyramid CrushVersion=\"1\" CrushStep=\"10\" CrushBits=\"8\" />"))!.DefaultWindow();

            Assert.AreEqual(0f, min);
            Assert.AreEqual(1f, max, 1e-6);
        }

        [TestMethod]
        public void AutoRangeClipsTheTails_SkippingTheEmptyBin_EvenWhenTheVolumeSentADisplayRange()
        {
            // 2000 pixels in codes 2..5 with a heavy dim tail: the tenth of a percent tails sit at the first and last used codes.
            XElement element = XElement.Parse(
                "<Pyramid CrushVersion=\"1\" CrushStep=\"10\" CrushBits=\"8\" CrushDisplayMin=\"0\" CrushDisplayMax=\"9\">" +
                "<CodeHistogram>1000000 0 500 500 500 500 0 0 0 0</CodeHistogram></Pyramid>");

            (double min, double max) = CrushInfo.FromElement(element)!.AutoRange();

            Assert.AreEqual(2.0, min);
            Assert.AreEqual(5.0, max);
        }

        [TestMethod]
        public void AutoRangeFallsBackToTheDefaultWindowWithoutAHistogram()
        {
            CrushInfo crush = CrushInfo.FromElement(XElement.Parse(
                "<Pyramid CrushVersion=\"1\" CrushStep=\"10\" CrushBits=\"8\" CrushDisplayMin=\"20\" CrushDisplayMax=\"49\" />"))!;

            Assert.AreEqual((20.0, 49.0), crush.AutoRange());
        }

        [TestMethod]
        public void MoreThanEightBitsIsNotSupported()
        {
            Assert.IsFalse(CrushInfo.FromElement(XElement.Parse("<Pyramid CrushVersion=\"1\" CrushStep=\"10\" CrushBits=\"9\" />"))!.IsSupported);
        }
    }
}
