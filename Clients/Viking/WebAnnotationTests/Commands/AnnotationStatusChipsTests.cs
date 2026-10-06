using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI;

namespace WebAnnotationTests.Commands
{
    [TestClass]
    public class AnnotationStatusChipsTests
    {
        [TestMethod]
        public void DescribePenReflectsOnOff()
        {
            Assert.AreEqual("Pen On", AnnotationStatusChips.DescribePen(true).Text);
            Assert.AreEqual("Pen Off", AnnotationStatusChips.DescribePen(false).Text);
            Assert.IsTrue(AnnotationStatusChips.DescribePen(true).EmphasizeOn);
            Assert.IsFalse(AnnotationStatusChips.DescribePen(false).EmphasizeOn);
        }

        [TestMethod]
        public void DescribeAutoPolygonizeBusyWinsOverOn()
        {
            Assert.AreEqual("AutoPoly Off", AnnotationStatusChips.DescribeAutoPolygonize(false, busy: true).Text);
            Assert.AreEqual("AutoPoly Busy", AnnotationStatusChips.DescribeAutoPolygonize(true, busy: true).Text);
            Assert.AreEqual("AutoPoly On", AnnotationStatusChips.DescribeAutoPolygonize(true, busy: false).Text);
            Assert.IsTrue(AnnotationStatusChips.DescribeAutoPolygonize(true, busy: true).EmphasizeBusy);
        }

        [TestMethod]
        public void DescribeAutoPolygonizePausedByZoomWinsOverOnButNotBusyOrOff()
        {
            Assert.AreEqual("AutoPoly Zoom", AnnotationStatusChips.DescribeAutoPolygonize(true, busy: false, pausedByZoom: true).Text);
            Assert.AreEqual("AutoPoly Busy", AnnotationStatusChips.DescribeAutoPolygonize(true, busy: true, pausedByZoom: true).Text);
            Assert.AreEqual("AutoPoly Off", AnnotationStatusChips.DescribeAutoPolygonize(false, busy: false, pausedByZoom: true).Text);
        }

        [TestMethod]
        public void DescribeSegmentationUnavailableWinsThenBusy()
        {
            Assert.AreEqual("Seg Off", AnnotationStatusChips.DescribeSegmentation(false, busy: true).Text);
            Assert.IsTrue(AnnotationStatusChips.DescribeSegmentation(false, busy: false).EmphasizeUnavailable);
            Assert.AreEqual("Seg Busy", AnnotationStatusChips.DescribeSegmentation(true, busy: true).Text);
            Assert.AreEqual("Seg Ready", AnnotationStatusChips.DescribeSegmentation(true, busy: false).Text);
            Assert.AreEqual("Seg Processing", AnnotationStatusChips.DescribeSegmentation(true, busy: false, processing: true).Text);
            Assert.AreEqual("Seg Busy", AnnotationStatusChips.DescribeSegmentation(true, busy: true, processing: true).Text);
        }
    }
}
