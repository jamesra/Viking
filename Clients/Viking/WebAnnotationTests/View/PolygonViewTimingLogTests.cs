using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using WebAnnotation.View;

namespace WebAnnotationTests.View
{
    /// <summary>Covers the threshold, claim-once, and line format of <see cref="PolygonViewTimingLog"/>.</summary>
    [TestClass]
    public class PolygonViewTimingLogTests
    {
        private string originalPath;
        private string tempPath;

        [TestInitialize]
        public void Setup()
        {
            originalPath = PolygonViewTimingLog.LogPath;
            tempPath = Path.Combine(Path.GetTempPath(), $"viking-polygon-timing-test-{Guid.NewGuid():N}.log");
            PolygonViewTimingLog.LogPath = tempPath;
        }

        [TestCleanup]
        public void Cleanup()
        {
            PolygonViewTimingLog.LogPath = originalPath;
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        private string ReadLog() => File.Exists(tempPath) ? File.ReadAllText(tempPath) : string.Empty;

        private static long Ticks(double milliseconds) => (long)(milliseconds * Stopwatch.Frequency / 1000.0);

        [TestMethod]
        public void TryBegin_WithoutMark_ReturnsNull()
        {
            Assert.IsNull(PolygonViewTimingLog.TryBegin(987654321));
        }

        [TestMethod]
        public void TryBegin_EveryViewInTheWindowGetsItsOwnTiming()
        {
            PolygonViewTimingLog.MarkBoundaryChange(111, "Retrace");

            PolygonViewTiming first = PolygonViewTimingLog.TryBegin(111);
            PolygonViewTiming second = PolygonViewTimingLog.TryBegin(111);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            StringAssert.Contains(ReadLog(), "polygon-view-claimed loc=111 view=2");
            StringAssert.Contains(ReadLog(), "polygon-view-caller loc=111 view=2");
        }

        [TestMethod]
        public void SlowView_IsLoggedWithBreakdown()
        {
            PolygonViewTimingLog.MarkBoundaryChange(222, "ChangeContour");
            PolygonViewTiming timing = PolygonViewTimingLog.TryBegin(222);

            timing.RecordConstruction(Ticks(20), vertices: 40, interiorRings: 1);
            timing.RecordInitialization(Ticks(60), Ticks(10), smoothedVertexCount: 800);
            Assert.IsFalse(ReadLog().Contains("polygon-view-slow"), "The slow line is written only after the mesh build reports.");
            timing.RecordTriangulation(TimeSpan.FromMilliseconds(200), succeeded: true);

            string line = ReadLog();
            StringAssert.Contains(line, "loc=222");
            StringAssert.Contains(line, "reason=ChangeContour");
            StringAssert.Contains(line, "verts_in=40");
            StringAssert.Contains(line, "verts_smoothed=800");
            StringAssert.Contains(line, "holes=1");
            StringAssert.Contains(line, "total_ms=290.0");
            StringAssert.Contains(line, "curve_ms=60.0");
            StringAssert.Contains(line, "delaunay_ms=200.0");
            StringAssert.Contains(line, "other_ms=30.0");
        }

        [TestMethod]
        public void FastView_IsClaimedButNotReportedSlow()
        {
            PolygonViewTimingLog.MarkBoundaryChange(333, "CutHole");
            PolygonViewTiming timing = PolygonViewTimingLog.TryBegin(333);

            timing.RecordConstruction(Ticks(5), vertices: 10, interiorRings: 0);
            timing.RecordInitialization(Ticks(5), Ticks(5), smoothedVertexCount: 100);
            timing.RecordTriangulation(TimeSpan.FromMilliseconds(20), succeeded: true);

            StringAssert.Contains(ReadLog(), "polygon-view-claimed loc=333");
            Assert.IsFalse(ReadLog().Contains("polygon-view-slow"));
            StringAssert.Contains(ReadLog(), "polygon-view-fast loc=333");
        }
    }
}
