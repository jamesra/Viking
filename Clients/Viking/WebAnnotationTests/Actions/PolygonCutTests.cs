using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using WebAnnotation.UI;
using WebAnnotation.UI.Actions;

namespace WebAnnotationTests.Actions
{
    /// <summary>
    /// Pins the pen retrace cut in <see cref="PolygonCut"/>: which retrace a crossing order means, how each side of a
    /// cut is built, which side a grow keeps, and the rejections that happen before the location store or the overlay
    /// is touched. Rejections are told apart by the reason <see cref="PolygonCut"/> writes to the trace log.
    /// </summary>
    [TestClass]
    public class PolygonCutTests
    {
        private const long LocID = 42;

        /// <summary>Areas here are sums of axis-aligned rectangles with integer corners, so they are exact up to rounding.</summary>
        private const double AreaTolerance = 1e-6;

        /// <summary>Same tolerance <see cref="PolygonCut.GetRetraceActionForPath"/> uses at downsample 1.</summary>
        private const double ReduceTolerance = 4;

        private static Vector2[] Square(double min, double max) =>
            [new(min, min), new(max, min), new(max, max), new(min, max), new(min, min)];

        private static Polygon UnitSquare => new(Square(0, 100));

        private static Polygon SquareWithHole
        {
            get
            {
                Polygon poly = new(Square(0, 100));
                poly.AddInteriorRing(new Polygon(Square(40, 60)));
                return poly;
            }
        }

        private static InteractionLogEvent Event(AnnotationRegionInteraction interaction, int index) => new(interaction, null, index);

        /// <summary>Runs the cut and returns the trace lines it wrote about <see cref="LocID"/>.</summary>
        private static (List<Change2DContourAction> Result, string Log) Cut(Polygon poly, Vector2[] path, InteractionLogEvent start, InteractionLogEvent next)
        {
            StringBuilder log = new();
            TraceListener listener = new StringBuilderListener(log);
            Trace.Listeners.Add(listener);
            try
            {
                List<Change2DContourAction> result = PolygonCut.GetRetraceActionForPath(LocID, poly, path, start, next);
                return (result, log.ToString());
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }
        }

        private sealed class StringBuilderListener(StringBuilder sb) : TraceListener
        {
            public override void Write(string message) => sb.Append(message);
            public override void WriteLine(string message) => sb.AppendLine(message);
        }

        // ---- GetRetraceCommandAction ----

        /// <summary>
        /// Entering the shape first means the stroke started outside and runs through the material, so it shrinks the
        /// outline; leaving first means it started inside and loops outside, so it grows. A hole is the reverse.
        /// </summary>
        [TestMethod]
        public void RetraceActionFollowsRingAndFirstInteraction()
        {
            Assert.AreEqual(RetraceCommandAction.SHRINK_EXTERIOR_RING, PolygonCut.GetRetraceCommandAction(IsInnerPoly: false, EnterIsFirstInteraction: true));
            Assert.AreEqual(RetraceCommandAction.GROW_EXTERIOR_RING, PolygonCut.GetRetraceCommandAction(IsInnerPoly: false, EnterIsFirstInteraction: false));
            Assert.AreEqual(RetraceCommandAction.GROW_INTERNAL_RING, PolygonCut.GetRetraceCommandAction(IsInnerPoly: true, EnterIsFirstInteraction: true));
            Assert.AreEqual(RetraceCommandAction.SHRINK_INTERNAL_RING, PolygonCut.GetRetraceCommandAction(IsInnerPoly: true, EnterIsFirstInteraction: false));
        }

        // ---- GetRetraceActionForPath rejections (before the store and overlay are read) ----

        [TestMethod]
        public void StartAtPathIndexZeroIsRejected()
        {
            Vector2[] path = [new(50, -10), new(50, 50), new(50, 110)];

            var (result, log) = Cut(UnitSquare, path, Event(AnnotationRegionInteraction.ENTER, 0), Event(AnnotationRegionInteraction.EXIT, 2));

            Assert.AreEqual(0, result.Count);
            StringAssert.Contains(log, $"location {LocID} offered no change: the start event is at path index 0");
        }

        /// <summary>A stroke that enters and ends inside crosses once, so there is nothing to cut.</summary>
        [TestMethod]
        public void SingleCrossingIsRejected()
        {
            Vector2[] path = [new(-10, 50), new(20, 50), new(50, 50)];

            var (result, log) = Cut(UnitSquare, path, Event(AnnotationRegionInteraction.ENTER, 1), Event(AnnotationRegionInteraction.EXIT, 2));

            Assert.AreEqual(0, result.Count);
            StringAssert.Contains(log, "path crosses the polygon outline 1 time(s), need 2 (events ENTER@1 to EXIT@2)");
        }

        [TestMethod]
        public void NoCrossingIsRejected()
        {
            Vector2[] path = [new(-30, 50), new(-20, 50), new(-10, 50)];

            var (result, log) = Cut(UnitSquare, path, Event(AnnotationRegionInteraction.ENTER, 1), Event(AnnotationRegionInteraction.EXIT, 2));

            Assert.AreEqual(0, result.Count);
            StringAssert.Contains(log, "path crosses the polygon outline 0 time(s)");
        }

        /// <summary>A cut cannot run from the outline into a hole; the two pieces would not be polygons.</summary>
        [TestMethod]
        public void CrossingsOnDifferentRingsAreRejected()
        {
            Vector2[] path = [new(-10, 50), new(10, 50), new(20, 50), new(30, 50), new(35, 50), new(50, 50)];

            var (result, log) = Cut(SquareWithHole, path, Event(AnnotationRegionInteraction.ENTER, 2), Event(AnnotationRegionInteraction.EXIT, 4));

            Assert.AreEqual(0, result.Count);
            StringAssert.Contains(log, "the first two crossings are on different rings");
        }

        /// <summary>
        /// The events' window (two points before the start to one after the next event) only sees the outline crossing.
        /// The cut then searches the whole path, finds the hole crossing too, and rejects on the ring check instead of the count.
        /// </summary>
        [TestMethod]
        public void WholePathIsSearchedWhenTheEventWindowMissesACrossing()
        {
            Vector2[] path = [new(-10, 50), new(10, 50), new(20, 50), new(30, 50), new(35, 50), new(50, 50)];

            var (result, log) = Cut(SquareWithHole, path, Event(AnnotationRegionInteraction.ENTER, 2), Event(AnnotationRegionInteraction.EXIT, 2));

            Assert.AreEqual(0, result.Count);
            StringAssert.Contains(log, "the first two crossings are on different rings");
            Assert.IsFalse(log.Contains("time(s)"), log);
        }

        // ---- TryCutSide ----

        /// <summary>
        /// A straight stroke across the square splits it; the two directions give the two different pieces, which tile
        /// the original exactly.
        /// </summary>
        [DataTestMethod]
        [DataRow(50.0)]
        [DataRow(30.0)]
        [DataRow(87.5)]
        public void CutSidesAreComplementaryPieces(double x)
        {
            Polygon square = UnitSquare;
            Vector2[] path = [new(x, -10), new(x, 50), new(x, 110)];

            Polygon cw = PolygonCut.TryCutSide(square, RotationDirection.Clockwise, path, ReduceTolerance, out string cwProblem);
            Polygon ccw = PolygonCut.TryCutSide(square, RotationDirection.Counterclockwise, path, ReduceTolerance, out string ccwProblem);

            Assert.IsNull(cwProblem);
            Assert.IsNull(ccwProblem);
            Assert.IsNotNull(cw);
            Assert.IsNotNull(ccw);

            double[] areas = [.. new[] { cw.Area, ccw.Area }.OrderBy(a => a)];
            double[] expected = [.. new[] { x * 100, (100 - x) * 100 }.OrderBy(a => a)];
            Assert.AreEqual(expected[0], areas[0], AreaTolerance);
            Assert.AreEqual(expected[1], areas[1], AreaTolerance);
            Assert.AreEqual(square.Area, cw.Area + ccw.Area, AreaTolerance);

            // The stroke's middle sample sits on the cut chord. The clockwise piece carries it mid-ring, where the
            // reduction drops it, leaving four corners plus the closing point; the counterclockwise piece carries it
            // just before the closing point, which ReduceExteriorRing always keeps.
            Assert.AreEqual(5, cw.ExteriorRing.Length, "clockwise piece: four corners plus the closing point");
            Assert.AreEqual(6, ccw.ExteriorRing.Length, "counterclockwise piece: four corners, the kept last sample, the closing point");

            // One piece lies left of the cut, the other right of it.
            Rectangle[] boxes = [.. new[] { cw.BoundingBox, ccw.BoundingBox }.OrderBy(b => b.Left)];
            Assert.AreEqual(0, boxes[0].Left, AreaTolerance);
            Assert.AreEqual(x, boxes[0].Right, AreaTolerance);
            Assert.AreEqual(x, boxes[1].Left, AreaTolerance);
            Assert.AreEqual(100, boxes[1].Right, AreaTolerance);
        }

        /// <summary>A stroke that never meets the ring has no cut; the side is reported with its direction instead of thrown.</summary>
        [TestMethod]
        public void CutSideThatMissesTheRingReportsTheDirection()
        {
            Vector2[] path = [new(-30, 50), new(-20, 50), new(-10, 50)];

            Polygon piece = PolygonCut.TryCutSide(UnitSquare, RotationDirection.Counterclockwise, path, ReduceTolerance, out string problem);

            Assert.IsNull(piece);
            Assert.IsNotNull(problem);
            StringAssert.StartsWith(problem, "Counterclockwise: ");
        }

        // ---- TryChooseGrownSide ----

        /// <summary>
        /// A stroke that leaves the top edge and comes back makes a 40x10 lobe. Growing keeps the square plus the lobe,
        /// never the lobe alone.
        /// </summary>
        [TestMethod]
        public void GrowKeepsTheOutlinePlusTheLobe()
        {
            Polygon square = UnitSquare;
            Vector2[] path = [new(30, 90), new(30, 110), new(70, 110), new(70, 90)];

            Polygon cw = PolygonCut.TryCutSide(square, RotationDirection.Clockwise, path, ReduceTolerance, out _);
            Polygon ccw = PolygonCut.TryCutSide(square, RotationDirection.Counterclockwise, path, ReduceTolerance, out _);

            Assert.IsTrue(PolygonCut.TryChooseGrownSide(square, cw, ccw, out bool useCcw));
            Polygon kept = useCcw ? ccw : cw;
            Polygon lobe = useCcw ? cw : ccw;
            Assert.AreEqual(10000 + 400, kept.Area, AreaTolerance);
            Assert.AreEqual(400, lobe.Area, AreaTolerance);
        }

        [TestMethod]
        public void BothSidesValidChoosesTheLarger()
        {
            Polygon source = new(Square(0, 10));
            Polygon small = new(Square(0, 5));
            Polygon large = new(Square(0, 20));

            Assert.IsTrue(PolygonCut.TryChooseGrownSide(source, small, large, out bool useCcw));
            Assert.IsTrue(useCcw, "counterclockwise is larger");

            Assert.IsTrue(PolygonCut.TryChooseGrownSide(source, large, small, out useCcw));
            Assert.IsFalse(useCcw, "clockwise is larger");
        }

        /// <summary>When one side failed validation, the lone piece is used only if it actually enlarges the source.</summary>
        [TestMethod]
        public void LoneValidSideIsUsedOnlyWhenItGrowsTheSource()
        {
            Polygon source = new(Square(0, 10));
            Polygon larger = new(Square(0, 20));
            Polygon lobe = new(Square(0, 5));

            Assert.IsTrue(PolygonCut.TryChooseGrownSide(source, larger, null, out bool useCcw));
            Assert.IsFalse(useCcw);

            Assert.IsTrue(PolygonCut.TryChooseGrownSide(source, null, larger, out useCcw));
            Assert.IsTrue(useCcw);

            Assert.IsFalse(PolygonCut.TryChooseGrownSide(source, lobe, null, out _));
            Assert.IsFalse(PolygonCut.TryChooseGrownSide(source, null, lobe, out _));
        }
    }
}
