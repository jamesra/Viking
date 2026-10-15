using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using WebAnnotation.UI.Commands;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class PenCancelButtonTests
    {
        [TestMethod]
        public void CentreHitsAndIsInTheTopRightCorner()
        {
            (double x, double y) = PenCancelButton.CenterOf(1200);
            Assert.IsTrue(PenCancelButton.HitTest(1200, x, y));
            Assert.IsTrue(x > 1200 / 2);
            Assert.IsTrue(y < PenCancelButton.ReservedTopPixels);
        }

        [TestMethod]
        public void StaysAtTheSameDistanceFromTheRightEdgeWhateverTheWidth()
        {
            Assert.AreEqual(1200 - PenCancelButton.CenterOf(1200).X, 800 - PenCancelButton.CenterOf(800).X, 1e-9);
        }

        [TestMethod]
        public void SitsInsideDockedChromeInsets()
        {
            var insets = new System.Windows.Forms.Padding(0, 40, 15, 60);
            (double x, double y) = PenCancelButton.CenterOf(1200, insets);
            (double x0, double y0) = PenCancelButton.CenterOf(1200);
            Assert.AreEqual(x0 - 15, x, 1e-9);
            Assert.AreEqual(y0 + 40, y, 1e-9);
            Assert.IsTrue(PenCancelButton.HitTest(1200, x, y, insets));
            Assert.IsFalse(PenCancelButton.HitTest(1200, x0, y0, insets));
        }

        [TestMethod]
        public void PointsOutsideTheDiscHitNothing()
        {
            (double x, double y) = PenCancelButton.CenterOf(1200);
            Assert.IsFalse(PenCancelButton.HitTest(1200, x - PenCancelButton.RadiusPixels - 1, y));
            Assert.IsFalse(PenCancelButton.HitTest(1200, x, y + PenCancelButton.RadiusPixels + 1));
            Assert.IsFalse(PenCancelButton.HitTest(1200, 100, 100));
        }
    }

    [TestClass]
    public class PenButtonStyleTests
    {
        [TestMethod]
        public void DarkDiscsGetWhiteTextAndLightDiscsGetBlackText()
        {
            Assert.AreEqual(Color.White, PenButtonStyle.LabelColorFor(new Color(30, 150, 60)));
            Assert.AreEqual(Color.White, PenButtonStyle.LabelColorFor(new Color(190, 40, 40)));
            Assert.AreEqual(Color.White, PenButtonStyle.LabelColorFor(Color.Black));
            Assert.AreEqual(Color.Black, PenButtonStyle.LabelColorFor(Color.Yellow));
            Assert.AreEqual(Color.Black, PenButtonStyle.LabelColorFor(Color.White));
            Assert.AreEqual(Color.Black, PenButtonStyle.LabelColorFor(new Color(200, 200, 200)));
        }

        [TestMethod]
        public void ShortWordsKeepTheFullFontAndLongOnesShrinkWithAFloor()
        {
            Assert.AreEqual(1.0, PenButtonStyle.LabelFontScale("Cut hole"), 1e-9);
            Assert.AreEqual(1.0, PenButtonStyle.LabelFontScale("Convert\n12345\nto polygon"), 1e-9);
            Assert.AreEqual(9.0 / 12.0, PenButtonStyle.LabelFontScale("Mitochondria"), 1e-9);
            Assert.AreEqual(0.6, PenButtonStyle.LabelFontScale("Supercalifragilisticexpialidocious"), 1e-9);
            Assert.AreEqual(1.0, PenButtonStyle.LabelFontScale(""), 1e-9);
        }
    }
}
