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

        [TestMethod]
        public void DescribeSegmentationErrorOutranksBusyButNotUnavailable()
        {
            var failing = AnnotationStatusChips.DescribeSegmentation(true, busy: true, processing: true, error: "server down");
            Assert.AreEqual("Seg Error", failing.Text);
            Assert.IsTrue(failing.EmphasizeError);
            Assert.AreEqual("Seg Off", AnnotationStatusChips.DescribeSegmentation(false, busy: false, error: "server down").Text);
            Assert.IsFalse(AnnotationStatusChips.DescribeSegmentation(true, busy: false).EmphasizeError);
        }

        [TestMethod]
        public void OnlyServerFailuresCountAsSegmentationErrors()
        {
            Assert.IsTrue(global::WebAnnotation.SegmentationServiceHealth.IsServiceFailure(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "down"))));
            Assert.IsTrue(global::WebAnnotation.SegmentationServiceHealth.IsServiceFailure(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.DeadlineExceeded, "slow"))));
            Assert.IsFalse(global::WebAnnotation.SegmentationServiceHealth.IsServiceFailure(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Cancelled, ""))));
            Assert.IsFalse(global::WebAnnotation.SegmentationServiceHealth.IsServiceFailure(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.FailedPrecondition, "NO_MATCHING_MASK"))));
        }

        [TestMethod]
        public void TrackerKeepsTheErrorUntilTheNextSuccess()
        {
            Viking.UI.ServiceErrorTracker tracker = new();
            int changes = 0;
            tracker.Changed += () => changes++;
            tracker.ReportFailure("no route");
            tracker.ReportFailure("no route");
            Assert.AreEqual("no route", tracker.LastError);
            Assert.AreEqual(1, changes);
            tracker.ReportSuccess();
            Assert.IsNull(tracker.LastError);
            Assert.AreEqual(2, changes);
        }
    }
}
