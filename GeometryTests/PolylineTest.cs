using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for PolylineTest
    /// </summary>
    [TestClass]
    public class PolylineTest
    {
        public PolylineTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get => testContextInstance;
            set => testContextInstance = value;
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestGenerator()
        {
            GeometryArbitraries.Register();

            Prop.ForAll<Polyline>((pl) =>
            {
                Trace.WriteLine(pl);
                bool NoSelfIntersection = pl.LineSegments.SelfIntersects(LineSetOrdering.Polyline) == false;
                bool OpenShape = pl.PointCount < 2 || pl.Points[0] != pl.Points[pl.Points.Count - 1];
                bool IsLine = pl.PointCount >= 2;
                bool pass = NoSelfIntersection && OpenShape;
                return pass.Classify(NoSelfIntersection == false, "Self intersection")
                           .Classify(OpenShape == false, "Closed shape")
                           .Trivial(false == IsLine);
            }).QuickCheckThrowOnFailure();
            //
            // TODO: Add test logic here
            //
        }

        [TestMethod]
        public void TestOperations()
        {
            GeometryArbitraries.Register();

            Configuration config = Configuration.VerboseThrowOnFailure;
            config.StartSize = 32;
            config.MaxNbOfTest = 1;

            PolylineSpec spec = new(Axis.X);
            spec.ToProperty().Check(config);
        }

        /// <summary>
        /// SliceGraph recenters via Translate, which uses the Vector2 constructor and used to leave rTree null.
        /// </summary>
        [TestMethod]
        public void AddPointsAtIntersections_AfterTranslate_InsertsCrossing()
        {
            Polyline a = new([new Vector2(0, 0), new Vector2(10, 10)]);
            Polyline b = new([new Vector2(0, 10), new Vector2(10, 0)]);
            Vector2 offset = new(1000, 2000);
            Polyline aT = a.Translate(offset);
            Polyline bT = b.Translate(offset);

            List<Vector2> hits = aT.AddPointsAtIntersections(bT);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(offset.X + 5, hits[0].X, 1e-6);
            Assert.AreEqual(offset.Y + 5, hits[0].Y, 1e-6);
        }

        /// <summary>
        /// Mixed-shape correspondence (used by SliceGraph) must survive translated polylines.
        /// </summary>
        [TestMethod]
        public void AddCorrespondingVertices_TranslatedPolylines_DoesNotThrow()
        {
            Polyline a = new([new Vector2(0, 0), new Vector2(10, 10)]);
            Polyline b = new([new Vector2(0, 10), new Vector2(10, 0)]);
            IShape2D[] shapes = [a.Translate(new Vector2(50, 50)), b.Translate(new Vector2(50, 50))];

            List<Vector2> added = shapes.AddCorrespondingVertices();

            Assert.IsTrue(added.Count >= 1);
        }

        /// <summary>
        /// Correspondence inserts on an interior edge. <see cref="Polyline.Insert"/> used to treat the previous/next
        /// vertex-sharing segments as a self-crossing, which aborted SliceGraph topology for real polylines.
        /// </summary>
        [TestMethod]
        public void AddPointsAtIntersections_FourVertexPolyline_InsertsOnInteriorEdge()
        {
            Polyline line = new(
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(20, 0),
                new Vector2(30, 0),
            ]);
            Polyline cutter = new(
            [
                new Vector2(15, -5),
                new Vector2(15, 5),
            ]);

            List<Vector2> hits = line.AddPointsAtIntersections(cutter);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(15, hits[0].X, 1e-6);
            Assert.AreEqual(0, hits[0].Y, 1e-6);
            Assert.AreEqual(5, line.PointCount);
            Assert.IsTrue(line.Points.Any(p => p.X == 15 && p.Y == 0));
        }

        /// <summary>
        /// Two multi-vertex polylines that cross must both receive the shared vertex (SliceGraph correspondence).
        /// </summary>
        [TestMethod]
        public void AddCorrespondingVertices_MultiVertexPolylines_DoesNotThrow()
        {
            Polyline a = new(
            [
                new Vector2(0, 0),
                new Vector2(0, 10),
                new Vector2(0, 20),
                new Vector2(0, 30),
            ]);
            Polyline b = new(
            [
                new Vector2(-10, 15),
                new Vector2(-5, 15),
                new Vector2(5, 15),
                new Vector2(10, 15),
            ]);
            IShape2D[] shapes = [a, b];

            List<Vector2> added = shapes.AddCorrespondingVertices();

            Assert.IsTrue(added.Count >= 1);
            Assert.AreEqual(5, a.PointCount);
            Assert.AreEqual(5, b.PointCount);
        }

        /// <summary>
        /// Inserting a vertex that actually crosses a non-adjacent segment must still be rejected.
        /// </summary>
        [TestMethod]
        public void Insert_PointThatCrossesNonAdjacentSegment_Throws()
        {
            Polyline line = new(
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
            ]);

            Assert.ThrowsException<ArgumentException>(() => line.Insert(1, new Vector2(5, 11)));
        }

        /// <summary>
        /// A CLOSEDCURVE annotation reaches Geometry as a polyline whose last point repeats its first, and the
        /// closing segment can cross the rest of the contour.  The bulk constructors do not validate, so such a
        /// polyline exists long before anybody inserts into it.  Splitting one of its segments must not be blamed
        /// for the crossing: RC1 cell 476 sections 271/272 lost its whole slice mesh to that misattribution.
        ///
        /// Coordinates are the contour that failed, so the arrangement is the real one rather than a guess at it.
        /// </summary>
        [TestMethod]
        public void Insert_OnPolylineThatAlreadySelfIntersects_DoesNotBlameTheInsertedPoint()
        {
            Polyline closedCurve = new(
            [
                new Vector2(-18260.986, -1152.505),
                new Vector2(-18190.273, -1160.419),
                new Vector2(-18133.112, -1177.182),
                new Vector2(-18062.325, -1211.564),
                new Vector2(-18028.617, -1226.130),
                new Vector2(-17964.889, -1241.967),
                new Vector2(-17878.412, -1246.284),
                new Vector2(-18260.986, -1152.505),
            ]);

            //Fixture check: the closing segment really does cross the contour, so the test exercises the bug.
            LineSegment closing = new(closedCurve.Points[6].ToVector2(), closedCurve.Points[7].ToVector2());
            LineSegment crossed = new(closedCurve.Points[2].ToVector2(), closedCurve.Points[3].ToVector2());
            Assert.IsTrue(closing.Intersects(crossed, EndpointsOnRingDoNotIntersect: false),
                "The contour must already self-intersect, or this test is not reproducing the reported failure.");

            //The point the correspondence pass inserted when the partner contour crossed the closing segment.
            closedCurve.Insert(7, new Vector2(-17945.400, -1229.864));

            Assert.AreEqual(9, closedCurve.PointCount);
            Assert.AreEqual(new Vector2(-17945.400, -1229.864), closedCurve.Points[7].ToVector2());
        }

        /// <summary>
        /// Prepending is a genuine extension rather than a split, so it has nothing to inherit an existing
        /// crossing from and must still be validated in full.
        /// </summary>
        [TestMethod]
        public void Insert_AtFrontCreatingCrossing_StillThrows()
        {
            Polyline line = new(
            [
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10),
            ]);

            Assert.ThrowsException<ArgumentException>(() => line.Insert(0, new Vector2(5, 11)));
        }
    }
}
