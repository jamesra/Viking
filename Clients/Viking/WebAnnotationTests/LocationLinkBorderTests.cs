using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using WebAnnotation.ViewModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// A location link (the hourglass between two markers) gets a thin border in a color that contrasts with the link, so
    /// it stands out from the image under it. Pins which color a link gets.
    /// </summary>
    [TestClass]
    public class LocationLinkBorderTests
    {
        [TestMethod]
        public void ALightLinkGetsABlackBorder()
        {
            Assert.AreEqual(Color.Black, LocationLinkView.BorderColorFor(new Color((byte)255, (byte)255, (byte)0)), "yellow");
            Assert.AreEqual(Color.Black, LocationLinkView.BorderColorFor(Color.White));
        }

        [TestMethod]
        public void ADarkLinkGetsAWhiteBorder()
        {
            Assert.AreEqual(Color.White, LocationLinkView.BorderColorFor(new Color((byte)0, (byte)0, (byte)255)), "blue");
            Assert.AreEqual(Color.White, LocationLinkView.BorderColorFor(Color.Black));
        }

        /// <summary>Green looks brighter than blue to the eye, so the same channel value must not decide both the same way.</summary>
        [TestMethod]
        public void BrightnessWeightsGreenMoreThanBlue()
        {
            Assert.AreEqual(Color.Black, LocationLinkView.BorderColorFor(new Color((byte)0, (byte)255, (byte)0)));
            Assert.AreEqual(Color.White, LocationLinkView.BorderColorFor(new Color((byte)0, (byte)0, (byte)255)));
        }

        [TestMethod]
        public void TheBorderColorIgnoresTheLinkAlpha()
        {
            Assert.AreEqual(Color.Black, LocationLinkView.BorderColorFor(new Color((byte)255, (byte)255, (byte)255, (byte)40)));
        }

        [TestMethod]
        public void TheBorderIsOpaqueAndThin()
        {
            Assert.AreEqual(255, LocationLinkView.BorderColorFor(Color.Red).A);
            Assert.IsTrue(LocationLinkView.BorderPixels > 0f && LocationLinkView.BorderPixels <= 3f);
        }
    }
}
