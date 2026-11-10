using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;

namespace VikingCoreTests
{
    /// <summary>
    /// The border round an arrow marker keeps the arrow's hue: fully saturated round a pale arrow, pale round a saturated one, and the
    /// label-outline color round a grey one.
    /// </summary>
    [TestClass]
    public class ArrowBorderColorTests
    {
        private static readonly Color GreyBorder = Color.White;

        [TestMethod]
        public void APaleArrowGetsTheFullySaturatedHue()
        {
            Assert.AreEqual(new Color(255, 0, 0), TextureCircleView.ArrowBorderColorFor(new Color(255, 180, 180), GreyBorder), "pink");
            Assert.AreEqual(new Color(0, 0, 255), TextureCircleView.ArrowBorderColorFor(new Color(60, 60, 100), GreyBorder), "dark slate blue");
        }

        [TestMethod]
        public void ASaturatedArrowGetsAPaleVersionOfItsHue()
        {
            Color border = TextureCircleView.ArrowBorderColorFor(new Color(0, 200, 0), GreyBorder);

            Assert.AreEqual(new Color(191, 255, 191), border, "green");
            Assert.AreEqual(new Color(255, 223, 191), TextureCircleView.ArrowBorderColorFor(new Color(255, 128, 0), GreyBorder), "orange");
        }

        [TestMethod]
        public void TheAlphaOfTheArrowDoesNotMatter()
        {
            Assert.AreEqual(
                TextureCircleView.ArrowBorderColorFor(new Color(255, 180, 180), GreyBorder),
                TextureCircleView.ArrowBorderColorFor(new Color((byte)255, (byte)180, (byte)180, (byte)64), GreyBorder));
        }

        [TestMethod]
        public void AGreyArrowKeepsTheLabelOutlineBorder()
        {
            Assert.AreEqual(GreyBorder, TextureCircleView.ArrowBorderColorFor(new Color(128, 128, 128), GreyBorder));
            Assert.AreEqual(Color.Black, TextureCircleView.ArrowBorderColorFor(Color.White, Color.Black));
            Assert.AreEqual(GreyBorder, TextureCircleView.ArrowBorderColorFor(Color.Black, GreyBorder));
        }
    }
}
