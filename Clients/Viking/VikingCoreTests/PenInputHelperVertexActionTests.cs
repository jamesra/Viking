using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;
using Vector2 = Geometry.Vector2;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="PenInputHelper.ApplyVertexAction"/>, the one place the pen path is edited, through the internal
    /// test constructor (no viewer): what each <see cref="PathVertexAction"/> does to the path, when
    /// <see cref="PenInputHelper.OnProposedNextSegmentChanged"/> fires, that <see cref="PenInputHelper.CanControlPointBePlaced"/>
    /// can veto an ADD, that segments stay one fewer than points (none on an emptied path, with no Debug.Assert
    /// failing), and that the simplify tolerance is capped.
    /// </summary>
    [TestClass]
    public class PenInputHelperVertexActionTests
    {
        /// <summary>
        /// Turns a failed <see cref="System.Diagnostics.Debug.Assert(bool)"/> into an exception. Inserted ahead of the
        /// default listener, whose assert dialog would otherwise block the test host. Only Debug builds of Geometry and
        /// VikingCore compile the asserts in, so the path's segment-count asserts are checked only there.
        /// </summary>
        private sealed class ThrowOnDebugAssert : System.Diagnostics.TraceListener
        {
            public ThrowOnDebugAssert() => System.Diagnostics.Trace.Listeners.Insert(0, this);

            public override void Fail(string message, string detailMessage) =>
                throw new InvalidOperationException($"Debug.Assert failed: {message} {detailMessage}");

            public override void Write(string message) { }

            public override void WriteLine(string message) { }

            protected override void Dispose(bool disposing)
            {
                System.Diagnostics.Trace.Listeners.Remove(this);
                base.Dispose(disposing);
            }
        }

        private ThrowOnDebugAssert? assertListener;

        [TestInitialize]
        public void FailTestsOnDebugAssert() => assertListener = new ThrowOnDebugAssert();

        [TestCleanup]
        public void RestoreDebugAssert() => assertListener?.Dispose();

        private static Vector2 V(double x, double y) => new(x, y);

        /// <summary>A helper holding a straight path along X: (0,0), (10,0), ... with <paramref name="count"/> points.</summary>
        private static PenInputHelper HelperWithLine(int count)
        {
            PenInputHelper helper = new();
            for (int i = 0; i < count; i++)
                helper.Push(V(i * 10, 0));

            return helper;
        }

        private static List<LineSegment?> CaptureProposedSegments(PenInputHelper helper)
        {
            List<LineSegment?> fired = [];
            helper.OnProposedNextSegmentChanged += (_, segment) => fired.Add(segment);
            return fired;
        }

        private static void AssertSegmentsMatchPoints(PenInputHelper helper)
        {
            Assert.AreEqual(Math.Max(0, helper.Points.Count - 1), helper.Segments.Count);
        }

        [TestMethod]
        public void None_ChangesNothingAndRaisesNothing()
        {
            PenInputHelper helper = HelperWithLine(3);
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            helper.ApplyVertexAction(PathVertexAction.NONE, V(99, 99));

            CollectionAssert.AreEqual(new[] { V(0, 0), V(10, 0), V(20, 0) }, helper.Points);
            Assert.AreEqual(0, fired.Count);
            AssertSegmentsMatchPoints(helper);
        }

        [TestMethod]
        public void None_AcceptsNullInput()
        {
            PenInputHelper helper = HelperWithLine(2);

            helper.ApplyVertexAction(PathVertexAction.NONE, null);

            Assert.AreEqual(2, helper.Points.Count);
        }

        [TestMethod]
        public void Add_OnEmptyPath_PushesPointAndReportsNoProposedSegment()
        {
            PenInputHelper helper = new();
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            helper.ApplyVertexAction(PathVertexAction.ADD, V(5, 5));

            CollectionAssert.AreEqual(new[] { V(5, 5) }, helper.Points);
            Assert.AreEqual(0, helper.Segments.Count);
            Assert.AreEqual(1, fired.Count);
            Assert.IsNull(fired[0]);
        }

        [TestMethod]
        public void Add_AppendsNewestPointAsSegmentA()
        {
            PenInputHelper helper = HelperWithLine(2);

            helper.ApplyVertexAction(PathVertexAction.ADD, V(20, 5));

            Assert.AreEqual(3, helper.Points.Count);
            Assert.AreEqual(V(20, 5), helper.Points.Last());
            Assert.AreEqual(2, helper.Segments.Count);
            Assert.AreEqual(V(20, 5), helper.Segments.Last().A, "Segments keep A as the newer control point.");
            Assert.AreEqual(V(10, 0), helper.Segments.Last().B);
        }

        [TestMethod]
        public void Add_RaisesProposedSegmentFromNewestPointToPenPosition()
        {
            PenInputHelper helper = HelperWithLine(1);
            helper.LastPenPosition = V(30, 0);
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            helper.ApplyVertexAction(PathVertexAction.ADD, V(10, 0));

            Assert.AreEqual(1, fired.Count);
            Assert.IsTrue(fired[0].HasValue);
            Assert.AreEqual(V(10, 0), fired[0]!.Value.A);
            Assert.AreEqual(V(30, 0), fired[0]!.Value.B);
        }

        [TestMethod]
        public void Add_WhenPenIsOnTheNewPoint_RaisesNullProposedSegment()
        {
            PenInputHelper helper = HelperWithLine(1);
            helper.LastPenPosition = V(10, 0);
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            helper.ApplyVertexAction(PathVertexAction.ADD, V(10, 0));

            Assert.AreEqual(1, fired.Count);
            Assert.IsNull(fired[0]);
        }

        [TestMethod]
        public void Add_VetoedByCanControlPointBePlaced_ChangesNothingAndRaisesNothing()
        {
            PenInputHelper helper = HelperWithLine(2);
            List<LineSegment?> fired = CaptureProposedSegments(helper);
            List<Vector2> asked = [];
            helper.CanControlPointBePlaced = p =>
            {
                asked.Add(p);
                return false;
            };

            helper.ApplyVertexAction(PathVertexAction.ADD, V(20, 5));

            CollectionAssert.AreEqual(new[] { V(20, 5) }, asked, "The veto sees the proposed position.");
            CollectionAssert.AreEqual(new[] { V(0, 0), V(10, 0) }, helper.Points);
            Assert.AreEqual(0, fired.Count);
            AssertSegmentsMatchPoints(helper);
        }

        [TestMethod]
        public void Add_ApprovedByCanControlPointBePlaced_Adds()
        {
            PenInputHelper helper = HelperWithLine(2);
            helper.CanControlPointBePlaced = p => p.Y > 0;

            helper.ApplyVertexAction(PathVertexAction.ADD, V(20, -1));
            Assert.AreEqual(2, helper.Points.Count);

            helper.ApplyVertexAction(PathVertexAction.ADD, V(20, 1));
            Assert.AreEqual(3, helper.Points.Count);
            Assert.AreEqual(V(20, 1), helper.Points.Last());
        }

        [TestMethod]
        public void Replace_SwapsNewestVertexAndKeepsSegmentCount()
        {
            PenInputHelper helper = HelperWithLine(3);
            helper.LastPenPosition = V(40, 0);
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            helper.ApplyVertexAction(PathVertexAction.REPLACE, V(20, 7));

            CollectionAssert.AreEqual(new[] { V(0, 0), V(10, 0), V(20, 7) }, helper.Points);
            Assert.AreEqual(V(20, 7), helper.Segments.Last().A);
            Assert.AreEqual(V(10, 0), helper.Segments.Last().B);
            AssertSegmentsMatchPoints(helper);
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual(V(20, 7), fired[0]!.Value.A);
            Assert.AreEqual(V(40, 0), fired[0]!.Value.B);
        }

        [TestMethod]
        public void Remove_PopsNewestVertexAndIgnoresInput()
        {
            PenInputHelper helper = HelperWithLine(3);
            helper.LastPenPosition = V(40, 0);
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            helper.ApplyVertexAction(PathVertexAction.REMOVE, null);

            CollectionAssert.AreEqual(new[] { V(0, 0), V(10, 0) }, helper.Points);
            AssertSegmentsMatchPoints(helper);
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual(V(10, 0), fired[0]!.Value.A, "The proposed segment now starts at the new newest vertex.");
        }

        [TestMethod]
        public void CutErase_RemovesVertexNearestInputAndEverythingAfterIt()
        {
            // EraseAlongLine relies on the nearest vertex itself going too: it erases at the crossed segment's newer end
            // and then ADDs the crossing point.
            PenInputHelper helper = HelperWithLine(4);
            helper.LastPenPosition = V(50, 0);
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            helper.ApplyVertexAction(PathVertexAction.CUT_ERASE, V(11, 1));

            CollectionAssert.AreEqual(new[] { V(0, 0) }, helper.Points);
            AssertSegmentsMatchPoints(helper);
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual(V(0, 0), fired[0]!.Value.A);
            Assert.AreEqual(V(50, 0), fired[0]!.Value.B);
        }

        [TestMethod]
        public void CutErase_NearNewestVertex_RemovesOnlyNewest()
        {
            PenInputHelper helper = HelperWithLine(4);

            helper.ApplyVertexAction(PathVertexAction.CUT_ERASE, V(31, 0));

            CollectionAssert.AreEqual(new[] { V(0, 0), V(10, 0), V(20, 0) }, helper.Points);
            AssertSegmentsMatchPoints(helper);
        }

        [TestMethod]
        public void Remove_LastVertex_EmptiesPathWithoutAssert()
        {
            PenInputHelper helper = HelperWithLine(1);
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            helper.ApplyVertexAction(PathVertexAction.REMOVE, null);

            Assert.AreEqual(0, helper.Points.Count);
            AssertSegmentsMatchPoints(helper);
            CollectionAssert.AreEqual(new LineSegment?[] { null }, fired);
        }

        [TestMethod]
        public void Replace_OnOnePointPath_SwapsThePointWithoutAssert()
        {
            PenInputHelper helper = HelperWithLine(1);

            helper.ApplyVertexAction(PathVertexAction.REPLACE, V(3, 4));

            CollectionAssert.AreEqual(new[] { V(3, 4) }, helper.Points);
            AssertSegmentsMatchPoints(helper);
        }

        [TestMethod]
        public void CutErase_AtFirstVertex_EmptiesPathWithoutAssert()
        {
            PenInputHelper helper = HelperWithLine(3);

            helper.ApplyVertexAction(PathVertexAction.CUT_ERASE, V(1, 1));

            Assert.AreEqual(0, helper.Points.Count);
            AssertSegmentsMatchPoints(helper);
        }

        [TestMethod]
        public void UndefinedAction_ThrowsArgumentExceptionAndChangesNothing()
        {
            PenInputHelper helper = HelperWithLine(3);
            List<LineSegment?> fired = CaptureProposedSegments(helper);

            Assert.ThrowsException<ArgumentException>(() => helper.ApplyVertexAction((PathVertexAction)99, V(1, 1)));

            Assert.AreEqual(3, helper.Points.Count);
            Assert.AreEqual(0, fired.Count);
        }

        [TestMethod]
        public void ActionsNeedingInput_NullInputThrowsAndChangesNothing()
        {
            foreach (PathVertexAction action in new[] { PathVertexAction.ADD, PathVertexAction.REPLACE, PathVertexAction.CUT_ERASE })
            {
                PenInputHelper helper = HelperWithLine(3);
                List<LineSegment?> fired = CaptureProposedSegments(helper);

                bool threw = false;
                try
                {
                    helper.ApplyVertexAction(action, null);
                }
                catch (Exception)
                {
                    threw = true;
                }

                Assert.IsTrue(threw, $"{action} with null input should throw.");
                Assert.AreEqual(3, helper.Points.Count, $"{action} must leave the path alone when it throws.");
                Assert.AreEqual(0, fired.Count, action.ToString());
            }
        }

        /// <summary>
        /// Every <see cref="PathVertexAction"/> must be classified here, so a new member fails this test until
        /// someone decides what <see cref="PenInputHelper.ApplyVertexAction"/> should do with it.
        /// </summary>
        [TestMethod]
        public void EveryDefinedAction_IsClassifiedAndHandled()
        {
            Dictionary<PathVertexAction, int> expectedPointDelta = new()
            {
                [PathVertexAction.NONE] = 0,
                [PathVertexAction.ADD] = 1,
                [PathVertexAction.REPLACE] = 0,
                [PathVertexAction.REMOVE] = -1,
                [PathVertexAction.CUT_ERASE] = -1,
            };

            foreach (PathVertexAction action in Enum.GetValues(typeof(PathVertexAction)))
            {
                Assert.IsTrue(expectedPointDelta.ContainsKey(action), $"{action} has no classification in this test.");

                PenInputHelper helper = HelperWithLine(3);
                helper.ApplyVertexAction(action, V(31, 3));

                Assert.AreEqual(3 + expectedPointDelta[action], helper.Points.Count, action.ToString());
                AssertSegmentsMatchPoints(helper);
            }
        }

        [TestMethod]
        public void RandomActionSequences_KeepSegmentsOneFewerThanPoints()
        {
            // Action 0..3 = ADD, REPLACE, REMOVE, CUT_ERASE. Points step along X so no two are equal (a zero-length
            // segment is not allowed). Edits run whenever the path has a vertex, so sequences also empty the path.
            var stepGen = Gen.Elements(0, 1, 2, 3);
            var offsetGen = Gen.Choose(0, 5);
            var yGen = Gen.Choose(-50, 50);
            var sequenceGen = Gen.ListOf(60, from a in stepGen
                                             from dx in offsetGen
                                             from y in yGen
                                             select (a, dx, y));

            Prop.ForAll(Arb.From(sequenceGen), steps =>
            {
                PenInputHelper helper = new();
                int counter = 0;
                foreach ((int a, int dx, int y) in steps)
                {
                    counter++;
                    Vector2 p = V(counter * 10 + dx, y);
                    switch (a)
                    {
                        case 0:
                            helper.ApplyVertexAction(PathVertexAction.ADD, p);
                            break;
                        case 1 when helper.Points.Count > 0:
                            helper.ApplyVertexAction(PathVertexAction.REPLACE, p);
                            break;
                        case 2 when helper.Points.Count > 0:
                            helper.ApplyVertexAction(PathVertexAction.REMOVE, null);
                            break;
                        case 3 when helper.Points.Count > 0:
                            Vector2 nearestVertex = helper.Points[dx % helper.Points.Count];
                            helper.ApplyVertexAction(PathVertexAction.CUT_ERASE, nearestVertex);
                            break;
                    }

                    Assert.AreEqual(Math.Max(0, helper.Points.Count - 1), helper.Segments.Count);
                }
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void SimplifiedPathTolerance_BelowCap_IsUsedAsGiven()
        {
            PenInputHelper helper = new();

            helper.SimplifiedPathToleranceInPixels = 0.5;

            Assert.AreEqual(0.5, helper.SimplifiedPathTolerance);
        }

        [TestMethod]
        public void SimplifiedPathTolerance_AtCap_IsUsedAsGiven()
        {
            PenInputHelper helper = new();

            helper.SimplifiedPathToleranceInPixels = PenInputHelper.MaxSimplifiedPathToleranceInVolumePixels;

            Assert.AreEqual(PenInputHelper.MaxSimplifiedPathToleranceInVolumePixels, helper.SimplifiedPathTolerance);
        }

        [TestMethod]
        public void SimplifiedPathTolerance_AboveCap_IsCappedAndRestoredWhenLowered()
        {
            PenInputHelper helper = new();

            helper.SimplifiedPathToleranceInPixels = 10;
            Assert.AreEqual(PenInputHelper.MaxSimplifiedPathToleranceInVolumePixels, helper.SimplifiedPathTolerance);
            Assert.AreEqual(10, helper.SimplifiedPathToleranceInPixels, "The requested value is kept; only the applied tolerance is capped.");

            helper.SimplifiedPathToleranceInPixels = 1.5;
            Assert.AreEqual(1.5, helper.SimplifiedPathTolerance);
        }

        [TestMethod]
        public void SimplifiedPathTolerance_AcrossMagnitudes_IsMinOfValueAndCap()
        {
            var valueGen = Gen.Elements(0.01, 0.5, 1.0, 1.999, 2.0, 2.001, 4.0, 100.0, 1e6);

            Prop.ForAll(Arb.From(valueGen), value =>
            {
                PenInputHelper helper = new();
                helper.SimplifiedPathToleranceInPixels = value;

                Assert.AreEqual(Math.Min(value, PenInputHelper.MaxSimplifiedPathToleranceInVolumePixels), helper.SimplifiedPathTolerance);
            }).QuickCheckThrowOnFailure();
        }
    }
}
