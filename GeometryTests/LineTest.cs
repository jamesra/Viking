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
    /// Summary description for LineTest
    /// </summary>
    [TestClass]
    public class LineTest
    {
        public LineTest()
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
        public void GridLineSegmentDistanceToPoint()
        {
            //Check edge conditions for a horizontal line
            {
                GridLineSegment lineA = new(new GridVector2(-5, 3),
                                                            new GridVector2(5, 3));

                //Check edge conditions for a horizontal line
                GridVector2 PointOnLine = new(2, 3);
                double Distance;
                Distance = lineA.DistanceToPoint(PointOnLine, out GridVector2 Intersection);
                Assert.AreEqual(0, Distance);
                Assert.IsTrue(Intersection == PointOnLine);

                //Check if we go past the line in X axis
                GridVector2 PointLeftOfLine = new(-10, 3);
                GridVector2 PointRightOfLine = new(10, 3);
                Distance = lineA.DistanceToPoint(PointLeftOfLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == lineA.A);

                Distance = lineA.DistanceToPoint(PointRightOfLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == lineA.B);

                //Check if we go above or below line
                GridVector2 PointAboveLine = new(3, 8);
                GridVector2 PointBelowLine = new(3, -2);
                Distance = lineA.DistanceToPoint(PointAboveLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == new GridVector2(3, 3));

                Distance = lineA.DistanceToPoint(PointBelowLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == new GridVector2(3, 3));
            }


            //Check edge conditions for a vertical line
            {
                GridLineSegment lineB = new(new GridVector2(3, -5),
                                                               new GridVector2(3, 5));

                GridVector2 PointOnLine = new(3, 2);
                double Distance;
                Distance = lineB.DistanceToPoint(PointOnLine, out GridVector2 Intersection);
                Assert.AreEqual(0, Distance);
                Assert.IsTrue(Intersection == PointOnLine);

                //Check if we go above or below line
                GridVector2 PointAboveLine = new(3, 10);
                GridVector2 PointBelowLine = new(3, -10);
                Distance = lineB.DistanceToPoint(PointAboveLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == lineB.B);

                Distance = lineB.DistanceToPoint(PointBelowLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == lineB.A);

                //Check if we go left or right of line
                GridVector2 PointLeftOfLine = new(-2, 4);
                GridVector2 PointRightOfLine = new(8, 4);
                Distance = lineB.DistanceToPoint(PointLeftOfLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == new GridVector2(3, 4));

                Distance = lineB.DistanceToPoint(PointRightOfLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == new GridVector2(3, 4));
            }

            {   //Check the diagonal line through the axis center
                GridLineSegment lineC = new(new GridVector2(-5, -5),
                                                               new GridVector2(5, 5));

                GridVector2 PointOnLine = new(0, 0);
                double Distance;
                Distance = lineC.DistanceToPoint(PointOnLine, out GridVector2 Intersection);
                Assert.AreEqual(0, Distance);
                Assert.IsTrue(Intersection == PointOnLine);

                GridVector2 PointOffLine = new(-5, 5);
                Distance = lineC.DistanceToPoint(PointOffLine, out Intersection);
                Assert.AreEqual(Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)), Distance);
                Assert.IsTrue(Intersection == new GridVector2(0, 0));

                GridVector2 PointPastEdge = new(-10, 0);
                Distance = lineC.DistanceToPoint(PointPastEdge, out Intersection);
                Assert.AreEqual(Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)), Distance);
                Assert.IsTrue(Intersection == new GridVector2(-5, -5));
            }

            {   //Check the diagonal line through the axis center
                GridLineSegment lineD = new(new GridVector2(-6, -4),
                                                               new GridVector2(4, 6));

                GridVector2 PointOnLine = new(-1, 1);
                double Distance;
                Distance = lineD.DistanceToPoint(PointOnLine, out GridVector2 Intersection);
                Assert.AreEqual(0, Distance);
                Assert.IsTrue(Intersection == PointOnLine);

                GridVector2 PointOffLine = new(-6, 6);
                Distance = lineD.DistanceToPoint(PointOffLine, out Intersection);
                Assert.AreEqual(Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)), Distance);
                Assert.IsTrue(Intersection == new GridVector2(-1, 1));

                GridVector2 PointPastEdge = new(9, 1);
                Distance = lineD.DistanceToPoint(PointPastEdge, out Intersection);
                Assert.AreEqual(Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)), Distance);
                Assert.IsTrue(Intersection == new GridVector2(4, 6));
            }
        }

        struct ExpectedLineIntersectionTest
        {
            public GridLineSegment Input;
            /// <summary>
            /// Null if no intersection expected
            /// </summary>
            public IShape2D Expected;
        }

        [TestMethod]
        public void GridLineSegmentHorizontalSimpleIntersects()
        {
            //
            // TODO: Add test logic	here
            //

            GridVector2 N9 = new(-9, 0);
            GridVector2 N5 = new(-5, 0);
            GridVector2 N1 = new(-1, 0);
            GridVector2 O = new(0, 0);
            GridVector2 P1 = new(1, 0);
            GridVector2 P5 = new(5, 0);
            GridVector2 P9 = new(9, 0);

            //The primary line we test against
            GridLineSegment Primary = new(N1, P1);
            GridLineSegment OP1 = new(O, P1);
            GridLineSegment N1O = new(N1, O);

            ExpectedLineIntersectionTest[] NoIntersectionTests =
            [
                new() { Expected = null, Input = new GridLineSegment(N9, N5) },
                new() { Expected = null, Input = new GridLineSegment(P5, P9) },
                new() { Expected = null, Input = Primary.Translate(GridVector2.UnitY) }, //Parallel but offset 
                new() { Expected = null, Input = Primary.Translate(-GridVector2.UnitY) } //Parallel but offset 
            ];

            ExpectedLineIntersectionTest[] EndpointOnlyIntersectionTests =
            [
                new() { Expected = N1, Input = new GridLineSegment(N9, N1) },
                new() { Expected = P1, Input = new GridLineSegment(P1, P9) }
            ];

            ExpectedLineIntersectionTest[] IntersectionTests =
            [
                new() { Expected = Primary, Input = Primary },
                new() { Expected = Primary, Input = new GridLineSegment(N5, P5) },
                new() { Expected = Primary, Input = new GridLineSegment(N1, P5) },
                new() { Expected = Primary, Input = new GridLineSegment(N5, P1) },
                new() { Expected = OP1, Input = OP1 },
                new() { Expected = N1O, Input = N1O },
                new() { Expected = OP1, Input = new GridLineSegment(O, P5) },
                new() { Expected = N1O, Input = new GridLineSegment(N5, O) },
            ];

            foreach (var test in NoIntersectionTests)
            {
                Assert.IsFalse(Primary.Intersects(test.Input));
            }

            foreach (var test in EndpointOnlyIntersectionTests)
            {
                var resultNoEndpointIntersection =
                    Primary.Intersects(test.Input, EndpointsOnRingDoNotIntersect: true, out IShape2D Intersection);
                Assert.IsFalse(resultNoEndpointIntersection);

                var resultWithEndpointIntersection =
                    Primary.Intersects(test.Input, EndpointsOnRingDoNotIntersect: false, out Intersection);
                Assert.IsTrue(resultWithEndpointIntersection);
                Assert.AreEqual(test.Expected, Intersection);
            }

            foreach (var test in IntersectionTests)
            {
                var result = Primary.Intersects(test.Input, out var intersection);
                Assert.IsTrue(result);
                Assert.IsTrue(test.Expected.Equals(intersection));
            }
        }


        [TestMethod]
        public void GridLineSegmentIntersects()
        {
            //
            // TODO: Add test logic	here
            //

            GridLineSegment lineA = new(new GridVector2(-5, 3),
                                                        new GridVector2(5, 3));
            GridLineSegment lineB = new(new GridVector2(3, -5),
                                                        new GridVector2(3, 5));
            GridLineSegment lineC = new(new GridVector2(-6, -5),
                                                        new GridVector2(-6, 5));
            GridLineSegment lineD = new(new GridVector2(-9, 8),
                                                        new GridVector2(1, -8));
            GridLineSegment lineE = new(new GridVector2(-9, 8),
                                                        new GridVector2(1, -2));

            bool result = lineA.Intersects(lineA, out IShape2D intersectShape);
            Assert.AreEqual(true, result);
            Assert.AreEqual(ShapeType2D.LINE, intersectShape.ShapeType);
            GridLineSegment intersectionLine = (GridLineSegment)intersectShape;
            Assert.IsTrue(intersectionLine == lineA);

            GridVector2 intersect;
            intersect = new GridVector2();
            result = lineA.Intersects(lineB, out intersect);
            Assert.AreEqual(true, result);
            Assert.IsTrue(intersect.X == 3 && intersect.Y == 3);

            result = lineA.Intersects(lineC, out intersect);
            Assert.AreEqual(false, result);

            result = lineA.Intersects(lineD, out intersect);
            Assert.AreEqual(false, result);
            //      Assert.IsTrue(intersect.X == -4 && intersect.Y == 3);

            result = lineA.Intersects(lineE, out intersect);
            Assert.AreEqual(true, result);
            Assert.IsTrue(intersect.X == -4 && intersect.Y == 3);
        }

        [TestMethod]
        public void GridLineSegmentInParallelIntersects()
        {
            //
            // TODO: Add test logic	here
            //

            GridLineSegment lineA = new(new GridVector2(-5, 5),
                                                        new GridVector2(5, 5));
            GridLineSegment lineB = new(new GridVector2(-7, 5),  //Total overlap, beyond both endpoints
                                                        new GridVector2(7, 5));
            GridLineSegment lineC = new(new GridVector2(-3, 5),  //Overlap, but not entirely
                                                        new GridVector2(3, 5));
            GridLineSegment lineD = new(new GridVector2(-10, 5),  //Endpoint Overlaps
                                                        new GridVector2(-5, 5));
            GridLineSegment lineE = new(new GridVector2(5, 5),    //Endpoint Overlaps
                                                        new GridVector2(10, 5));
            GridLineSegment lineF = new(new GridVector2(-5, 4), //Parrallel, but slightly above
                                                        new GridVector2(5, 4));
            GridLineSegment lineG = new(new GridVector2(-5, 6), //Parallel, but slightly below
                                                        new GridVector2(5, 6));

            GridLineSegment[] IntersectingLines = [lineB, lineC, lineD, lineE];
            GridLineSegment[] NonIntersectingLines = [lineF, lineG];


            foreach (GridLineSegment other in IntersectingLines)
            {
                bool result = lineA.Intersects(other, out IShape2D intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingLines)
            {
                bool result = lineA.Intersects(other, out GridVector2 intersection);
                Assert.IsFalse(result);
            }

            GridLineSegment vertLine = new(new GridVector2(lineA.A.Y, lineA.A.X), new GridVector2(lineA.B.Y, lineA.B.X));

            GridLineSegment[] IntersectingVertical = [.. IntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X)))];
            GridLineSegment[] NonIntersectingVertical = [.. NonIntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X)))];

            foreach (GridLineSegment other in IntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out GridVector2 intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out GridVector2 intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineSegmentInParallelIntersects2()
        {
            //
            // TODO: Add test logic	here
            //

            GridLineSegment lineA = new(new GridVector2(0, 10),
                                                        new GridVector2(0, -10));
            GridLineSegment lineB = new(new GridVector2(0, 11),  //Total overlap, beyond both endpoints
                                                        new GridVector2(0, -11));
            GridLineSegment lineC = new(new GridVector2(0, 3),  //Overlap, but not entirely
                                                        new GridVector2(0, 15));
            GridLineSegment lineD = new(new GridVector2(0, 10),  //Endpoint Overlaps
                                                        new GridVector2(0, 15));
            GridLineSegment lineE = new(new GridVector2(0, -10),    //Endpoint Overlaps
                                                        new GridVector2(0, -15));
            GridLineSegment lineF = new(new GridVector2(1, 10), //Parrallel, but slightly right
                                                        new GridVector2(1, -10));
            GridLineSegment lineG = new(new GridVector2(-1, 10), //Parallel, but slightly left
                                                        new GridVector2(-1, -10));


            GridLineSegment[] IntersectingLines = [lineB, lineC, lineD, lineE];
            GridLineSegment[] NonIntersectingLines = [lineF, lineG];

            foreach (GridLineSegment other in IntersectingLines)
            {
                bool result = lineA.Intersects(other, out IShape2D intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingLines)
            {
                bool result = lineA.Intersects(other, out GridVector2 intersection);
                Assert.IsFalse(result);
            }

            GridLineSegment vertLine = new(new GridVector2(lineA.A.Y, lineA.A.X), new GridVector2(lineA.B.Y, lineA.B.X));

            GridLineSegment[] IntersectingVertical = [.. IntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X)))];
            GridLineSegment[] NonIntersectingVertical = [.. NonIntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X)))];

            foreach (GridLineSegment other in IntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out GridVector2 intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out GridVector2 intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineSegmentInParallelIntersects3()
        {
            //
            // TODO: Add test logic	here
            //

            GridLineSegment lineA = new(new GridVector2(0, 0),
                                                        new GridVector2(10, 10));
            GridLineSegment lineB = new(new GridVector2(-1, -1),  //Total overlap, beyond both endpoints
                                                        new GridVector2(11, 11));
            GridLineSegment lineC = new(new GridVector2(3, 3),  //Overlap, but not entirely
                                                        new GridVector2(15, 15));
            GridLineSegment lineD = new(new GridVector2(10, 10),  //Endpoint Overlaps
                                                        new GridVector2(15, 15));
            GridLineSegment lineE = new(new GridVector2(-10, -10),    //Endpoint Overlaps
                                                        new GridVector2(0, 0));
            GridLineSegment lineF = new(new GridVector2(0, -1), //Parrallel, but slightly right
                                                        new GridVector2(10, 9));
            GridLineSegment lineG = new(new GridVector2(0, 1), //Parallel, but slightly left
                                                        new GridVector2(10, 11));


            GridLineSegment[] IntersectingLines = [lineB, lineC, lineD, lineE];
            GridLineSegment[] NonIntersectingLines = [lineF, lineG];

            foreach (GridLineSegment other in IntersectingLines)
            {
                bool result = lineA.Intersects(other, out IShape2D intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingLines)
            {
                bool result = lineA.Intersects(other, out GridVector2 intersection);
                Assert.IsFalse(result);
            }

            GridLineSegment vertLine = new(new GridVector2(lineA.A.Y, lineA.A.X), new GridVector2(lineA.B.Y, lineA.B.X));

            GridLineSegment[] IntersectingVertical = [.. IntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X)))];
            GridLineSegment[] NonIntersectingVertical = [.. NonIntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X)))];

            foreach (GridLineSegment other in IntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out GridVector2 intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out GridVector2 intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineIntersects()
        {
            //
            // TODO: Add test logic	here
            //

            GridLine lineA = new(new GridVector2(-5, 0),
                                                        new GridVector2(-10, 0));
            GridLine lineB = new(new GridVector2(0, 5),
                                                        new GridVector2(0, -5));

            GridVector2 intersect = new();
            bool result = lineA.Intersects(lineB, out intersect);
            Assert.AreEqual(true, result);
            Assert.IsTrue(intersect.X == 0 && intersect.Y == 0);
        }

        [TestMethod]
        public void LineSetIntersectionsTest()
        {
            //Create a line mostly along the X axis.  Split it at x=2.5 and x=7.5.  Ensure we get three line segments and two intersection points
            GridVector2 A = new(0, 0);
            GridVector2 B = new(10, 1);

            GridLineSegment line = new(A, B);

            GridLineSegment[] OtherLines = [ new(new GridVector2(2.5, 0), new GridVector2(2.5, 10)),
                                                                   new(new GridVector2(0, 11), new GridVector2(10,11)), //A line that doesn't intersect
                                                                   new(new GridVector2(7.5, 0), new GridVector2(7.5, 10)) ];

            List<GridLineSegment> intersectingLines = line.Intersections(OtherLines, out GridVector2[] splitPoints);

            GridVector2 ExpectedIntersectionA = new(2.5, 0.25);
            GridVector2 ExpectedIntersectionB = new(7.5, 0.75);

            Assert.AreEqual(2, splitPoints.Length);
            Assert.AreEqual(ExpectedIntersectionA, splitPoints[0]);
            Assert.AreEqual(ExpectedIntersectionB, splitPoints[1]);

            /*
            GridLineSegment[] expectedLines = new GridLineSegment[] { new GridLineSegment(A, ExpectedIntersectionA),
                                                                           new GridLineSegment(ExpectedIntersectionA, ExpectedIntersectionB),
                                                                           new GridLineSegment(ExpectedIntersectionB, B) };
                                                                           */
            GridLineSegment[] expectedLines = [OtherLines[0], OtherLines[2]];

            Assert.AreEqual(2, intersectingLines.Count);

            for (int i = 0; i < intersectingLines.Count; i++)
            {
                Assert.AreEqual(intersectingLines[i], expectedLines[i]);
            }
        }

        /// <summary>
        /// Divide a line at two points in the middle and ensure the results are in order.
        /// </summary>
        [TestMethod]
        public void SubdivideLineTest()
        {
            //Create a line mostly along the X axis.  Split it at x=2.5 and x=7.5.  Ensure we get three line segments and two intersection points
            GridVector2 A = new(0, 0);
            GridVector2 B = new(10, 1);

            GridLineSegment line = new(A, B);

            GridLineSegment[] OtherLines = [ new(new GridVector2(2.5, 0), new GridVector2(2.5, 10)),
                                                                   new(new GridVector2(0, 11), new GridVector2(10,11)), //A line that doesn't intersect
                                                                   new(new GridVector2(7.5, 0), new GridVector2(7.5, 10)) ];

            List<GridLineSegment> dividedLines = line.SubdivideAtIntersections(OtherLines, out GridVector2[] splitPoints);

            GridVector2 ExpectedIntersectionA = new(2.5, 0.25);
            GridVector2 ExpectedIntersectionB = new(7.5, 0.75);

            Assert.AreEqual(2, splitPoints.Length);
            Assert.AreEqual(ExpectedIntersectionA, splitPoints[0]);
            Assert.AreEqual(ExpectedIntersectionB, splitPoints[1]);


            GridLineSegment[] expectedLines = [ new(A, ExpectedIntersectionA),
                                                                           new(ExpectedIntersectionA, ExpectedIntersectionB),
                                                                           new(ExpectedIntersectionB, B) ];

            Assert.AreEqual(3, dividedLines.Count);

            for (int i = 0; i < dividedLines.Count; i++)
            {
                Assert.AreEqual(dividedLines[i], expectedLines[i]);
            }
        }

        /// <summary>
        /// Ensure that if we intersect at the endpoint we do not get an extra line
        /// </summary>
        [TestMethod]
        public void SubdivideLineTestAtEndpoints()
        {
            //Create a line mostly along the X axis.  Split it at x=2.5 and x=7.5.  Ensure we get three line segments and two intersection points
            GridVector2 A = new(0, 0);
            GridVector2 B = new(10, 1);

            GridLineSegment line = new(A, B);

            GridLineSegment[] OtherLines = [ new(new GridVector2(0, -1), new GridVector2(0, 10)),
                                                                   new(new GridVector2(0, 11), new GridVector2(10,11)), //A line that doesn't intersect
                                                                   new(new GridVector2(10, 0), new GridVector2(10, 10)) ];

            List<GridLineSegment> dividedLines = line.SubdivideAtIntersections(OtherLines, out GridVector2[] splitPoints);

            Assert.AreEqual(0, splitPoints.Length);

            GridLineSegment[] expectedLines = [new(A, B)];

            Assert.AreEqual(1, dividedLines.Count);

            for (int i = 0; i < dividedLines.Count; i++)
            {
                Assert.AreEqual(dividedLines[i], expectedLines[i]);
            }
        }

        /*
        public void TestSubdivideWithFSCheck()
        {
            Func<double, GridLineSegment, bool> subdivide_check = (val, line) =>
            {
                GridVector2 linePoint = line.PointAlongLine(val);

            };
        }*/

        [TestMethod]
        public void TestIsLeft()
        {
            //Is a point to the left when standing at A looking at B
            GridVector2 A = new(0, 0);
            GridVector2 B = new(10, 0);
            GridLineSegment line = new(A, B);

            GridVector2 left = new(0, 1);
            GridVector2 right = new(0, -1);
            GridVector2 on = A;

            Assert.AreEqual(1, line.IsLeft(left));
            Assert.AreEqual(-1, line.IsLeft(right));
            Assert.AreEqual(0, line.IsLeft(on));

            left = new GridVector2(-1, 1);
            right = new GridVector2(-1, -1);
            on = new GridVector2(5, 0);

            Assert.AreEqual(1, line.IsLeft(left));
            Assert.AreEqual(-1, line.IsLeft(right));
            Assert.AreEqual(0, line.IsLeft(on));

            left = new GridVector2(11, 1);
            right = new GridVector2(11, -1);
            on = new GridVector2(11, 0);

            Assert.AreEqual(1, line.IsLeft(left));
            Assert.AreEqual(-1, line.IsLeft(right));
            Assert.AreEqual(0, line.IsLeft(on));

            on = new GridVector2(-1, 0);
            Assert.AreEqual(0, line.IsLeft(on));
        }

        [TestMethod]
        public void TestIsLeftWithFSCheck()
        {
            Arb.Register<GridVector2Generators>();

            bool IsLeftCheck(GridVector2 p, GridVector2 q, GridVector2 r)
            {
                if (p == q || q == r || r == p)
                    return true;

                GridLineSegment pq = new(p, q);
                GridLineSegment pr = new(p, r);

                Trace.WriteLine(string.Format("{0} , {1}", pq, pr));
                int r_isleft = pq.IsLeft(r);
                Assert.IsTrue(r_isleft >= -1);
                Assert.IsTrue(r_isleft <= 1);

                int q_isleft = pr.IsLeft(q);
                Assert.IsTrue(q_isleft >= -1);
                Assert.IsTrue(q_isleft <= 1);

                if (r_isleft == 0)
                {
                    Assert.AreEqual(q_isleft, r_isleft);
                    return q_isleft == r_isleft;
                }
                else
                {
                    Assert.AreEqual(-q_isleft, r_isleft);
                    return -q_isleft == r_isleft;
                }
            }

            Prop.ForAll<GridVector2, GridVector2, GridVector2>(IsLeftCheck).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void TestIsLeftWithFSCheckOnHorizontalLine()
        {
            Arb.Register<GridVector2Generators>();

            bool IsLeftCheck(GridVector2 p)
            {
                GridLineSegment qr = new(new GridVector2(-10, 0), new GridVector2(10, 0));
                GridLineSegment rq = new(new GridVector2(10, 0), new GridVector2(-10, 0));

                Trace.WriteLine(string.Format("{0} , {1}", qr, p));
                int qr_p_isleft = qr.IsLeft(p);
                int qr_p_ExpectedLeft = p.Y == 0 ? 0 : p.Y < 0 ? -1 : 1;

                Assert.AreEqual(qr_p_isleft, qr_p_ExpectedLeft);

                //We expect the opposite result if we reverse the line
                Trace.WriteLine(string.Format("{0} , {1}", rq, p));
                int rq_p_isleft = rq.IsLeft(p);
                int rq_p_ExpectedLeft = p.Y == 0 ? 0 : p.Y > 0 ? -1 : 1;

                Assert.AreEqual(rq_p_isleft, rq_p_ExpectedLeft);

                Assert.AreEqual(-qr_p_ExpectedLeft, rq_p_ExpectedLeft);
                return rq_p_isleft == rq_p_ExpectedLeft;
            }

            Prop.ForAll<GridVector2>(IsLeftCheck).QuickCheckThrowOnFailure();
        }

    }
}
