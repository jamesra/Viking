using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class SegmentationPenButtonsTests
    {
        private const int Width = 1200;

        [TestMethod]
        public void SkipIsOfferedOnlyWithAFallbackPolygon()
        {
            CollectionAssert.AreEqual(
                new[] { SegmentationPenButton.Accept, SegmentationPenButton.Skip },
                SegmentationPenButtons.Layout(includeSkip: true).ToArray());
            CollectionAssert.AreEqual(
                new[] { SegmentationPenButton.Accept },
                SegmentationPenButtons.Layout(includeSkip: false).ToArray());
        }

        [TestMethod]
        public void EachButtonCentreHitsItselfAndNoOther()
        {
            var buttons = SegmentationPenButtons.Layout(includeSkip: true);
            for (int i = 0; i < buttons.Count; i++)
            {
                (double x, double y) = SegmentationPenButtons.CenterOf(i, Width);
                Assert.AreEqual(buttons[i], SegmentationPenButtons.HitTest(true, Width, x, y));
            }
        }

        [TestMethod]
        public void PointsOutsideEveryButtonHitNothing()
        {
            Assert.IsNull(SegmentationPenButtons.HitTest(true, Width, 100, 100));
            (double x, double y) = SegmentationPenButtons.CenterOf(0, Width);
            Assert.IsNull(SegmentationPenButtons.HitTest(true, Width, x - SegmentationPenButtons.RadiusPixels - 1, y));
        }

        [TestMethod]
        public void ButtonsSitBelowTheCancelButton()
        {
            (double _, double firstY) = SegmentationPenButtons.CenterOf(0, Width);
            (double _, double cancelY) = PenCancelButton.CenterOf(Width);
            Assert.IsTrue(firstY - SegmentationPenButtons.RadiusPixels >= cancelY + PenCancelButton.RadiusPixels);
        }
    }
}
