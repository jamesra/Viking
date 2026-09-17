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
                LineSegment lineA = new(new Vector2(-5, 3),
                                                            new Vector2(5, 3));

                //Check edge conditions for a horizontal line
                Vector2 PointOnLine = new(2, 3);
                double Distance;
                Distance = lineA.DistanceToPoint(PointOnLine, out Vector2 Intersection);
                Assert.AreEqual(0, Distance);
                Assert.IsTrue(Intersection == PointOnLine);

                //Check if we go past the line in X axis
                Vector2 PointLeftOfLine = new(-10, 3);
                Vector2 PointRightOfLine = new(10, 3);
                Distance = lineA.DistanceToPoint(PointLeftOfLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == lineA.A);

                Distance = lineA.DistanceToPoint(PointRightOfLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == lineA.B);

                //Check if we go above or below line
                Vector2 PointAboveLine = new(3, 8);
                Vector2 PointBelowLine = new(3, -2);
                Distance = lineA.DistanceToPoint(PointAboveLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == new Vector2(3, 3));

                Distance = lineA.DistanceToPoint(PointBelowLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == new Vector2(3, 3));
            }


            //Check edge conditions for a vertical line
            {
                LineSegment lineB = new(new Vector2(3, -5),
                                                               new Vector2(3, 5));

                Vector2 PointOnLine = new(3, 2);
                double Distance;
                Distance = lineB.DistanceToPoint(PointOnLine, out Vector2 Intersection);
                Assert.AreEqual(0, Distance);
                Assert.IsTrue(Intersection == PointOnLine);

                //Check if we go above or below line
                Vector2 PointAboveLine = new(3, 10);
                Vector2 PointBelowLine = new(3, -10);
                Distance = lineB.DistanceToPoint(PointAboveLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == lineB.B);

                Distance = lineB.DistanceToPoint(PointBelowLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == lineB.A);

                //Check if we go left or right of line
                Vector2 PointLeftOfLine = new(-2, 4);
                Vector2 PointRightOfLine = new(8, 4);
                Distance = lineB.DistanceToPoint(PointLeftOfLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == new Vector2(3, 4));

                Distance = lineB.DistanceToPoint(PointRightOfLine, out Intersection);
                Assert.AreEqual(5, Distance);
                Assert.IsTrue(Intersection == new Vector2(3, 4));
            }

            {   //Check the diagonal line through the axis center
                LineSegment lineC = new(new Vector2(-5, -5),
                                                               new Vector2(5, 5));

                Vector2 PointOnLine = new(0, 0);
                double Distance;
                Distance = lineC.DistanceToPoint(PointOnLine, out Vector2 Intersection);
                Assert.AreEqual(0, Distance);
                Assert.IsTrue(Intersection == PointOnLine);

                Vector2 PointOffLine = new(-5, 5);
                Distance = lineC.DistanceToPoint(PointOffLine, out Intersection);
                Assert.AreEqual(Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)), Distance);
                Assert.IsTrue(Intersection == new Vector2(0, 0));

                Vector2 PointPastEdge = new(-10, 0);
                Distance = lineC.DistanceToPoint(PointPastEdge, out Intersection);
                Assert.AreEqual(Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)), Distance);
                Assert.IsTrue(Intersection == new Vector2(-5, -5));
            }

            {   //Check the diagonal line through the axis center
                LineSegment lineD = new(new Vector2(-6, -4),
                                                               new Vector2(4, 6));

                Vector2 PointOnLine = new(-1, 1);
                double Distance;
                Distance = lineD.DistanceToPoint(PointOnLine, out Vector2 Intersection);
                Assert.AreEqual(0, Distance);
                Assert.IsTrue(Intersection == PointOnLine);

                Vector2 PointOffLine = new(-6, 6);
                Distance = lineD.DistanceToPoint(PointOffLine, out Intersection);
                Assert.AreEqual(Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)), Distance);
                Assert.IsTrue(Intersection == new Vector2(-1, 1));

                Vector2 PointPastEdge = new(9, 1);
                Distance = lineD.DistanceToPoint(PointPastEdge, out Intersection);
                Assert.AreEqual(Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)), Distance);
                Assert.IsTrue(Intersection == new Vector2(4, 6));
            }
        }

        struct ExpectedLineIntersectionTest
        {
            public LineSegment Input;
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

            Vector2 N9 = new(-9, 0);
            Vector2 N5 = new(-5, 0);
            Vector2 N1 = new(-1, 0);
            Vector2 O = new(0, 0);
            Vector2 P1 = new(1, 0);
            Vector2 P5 = new(5, 0);
            Vector2 P9 = new(9, 0);

            //The primary line we test against
            LineSegment Primary = new(N1, P1);
            LineSegment OP1 = new(O, P1);
            LineSegment N1O = new(N1, O);

            ExpectedLineIntersectionTest[] NoIntersectionTests =
            [
                new() { Expected = null, Input = new LineSegment(N9, N5) },
                new() { Expected = null, Input = new LineSegment(P5, P9) },
                new() { Expected = null, Input = Primary.Translate(Vector2.UnitY) }, //Parallel but offset 
                new() { Expected = null, Input = Primary.Translate(-Vector2.UnitY) } //Parallel but offset 
            ];

            ExpectedLineIntersectionTest[] EndpointOnlyIntersectionTests =
            [
                new() { Expected = N1, Input = new LineSegment(N9, N1) },
                new() { Expected = P1, Input = new LineSegment(P1, P9) }
            ];

            ExpectedLineIntersectionTest[] IntersectionTests =
            [
                new() { Expected = Primary, Input = Primary },
                new() { Expected = Primary, Input = new LineSegment(N5, P5) },
                new() { Expected = Primary, Input = new LineSegment(N1, P5) },
                new() { Expected = Primary, Input = new LineSegment(N5, P1) },
                new() { Expected = OP1, Input = OP1 },
                new() { Expected = N1O, Input = N1O },
                new() { Expected = OP1, Input = new LineSegment(O, P5) },
                new() { Expected = N1O, Input = new LineSegment(N5, O) },
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

            LineSegment lineA = new(new Vector2(-5, 3),
                                                        new Vector2(5, 3));
            LineSegment lineB = new(new Vector2(3, -5),
                                                        new Vector2(3, 5));
            LineSegment lineC = new(new Vector2(-6, -5),
                                                        new Vector2(-6, 5));
            LineSegment lineD = new(new Vector2(-9, 8),
                                                        new Vector2(1, -8));
            LineSegment lineE = new(new Vector2(-9, 8),
                                                        new Vector2(1, -2));

            bool result = lineA.Intersects(lineA, out IShape2D intersectShape);
            Assert.AreEqual(true, result);
            Assert.AreEqual(ShapeType2D.Line, intersectShape.ShapeType);
            LineSegment intersectionLine = (LineSegment)intersectShape;
            Assert.IsTrue(intersectionLine == lineA);

            Vector2 intersect;
            intersect = new Vector2();
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

            LineSegment lineA = new(new Vector2(-5, 5),
                                                        new Vector2(5, 5));
            LineSegment lineB = new(new Vector2(-7, 5),  //Total overlap, beyond both endpoints
                                                        new Vector2(7, 5));
            LineSegment lineC = new(new Vector2(-3, 5),  //Overlap, but not entirely
                                                        new Vector2(3, 5));
            LineSegment lineD = new(new Vector2(-10, 5),  //Endpoint Overlaps
                                                        new Vector2(-5, 5));
            LineSegment lineE = new(new Vector2(5, 5),    //Endpoint Overlaps
                                                        new Vector2(10, 5));
            LineSegment lineF = new(new Vector2(-5, 4), //Parrallel, but slightly above
                                                        new Vector2(5, 4));
            LineSegment lineG = new(new Vector2(-5, 6), //Parallel, but slightly below
                                                        new Vector2(5, 6));

            LineSegment[] IntersectingLines = [lineB, lineC, lineD, lineE];
            LineSegment[] NonIntersectingLines = [lineF, lineG];


            foreach (LineSegment other in IntersectingLines)
            {
                bool result = lineA.Intersects(other, out IShape2D intersection);
                Assert.IsTrue(result);
            }

            foreach (LineSegment other in NonIntersectingLines)
            {
                bool result = lineA.Intersects(other, out Vector2 intersection);
                Assert.IsFalse(result);
            }

            LineSegment vertLine = new(new Vector2(lineA.A.Y, lineA.A.X), new Vector2(lineA.B.Y, lineA.B.X));

            LineSegment[] IntersectingVertical = [.. IntersectingLines.Select(l => new LineSegment(new Vector2(l.A.Y, l.A.X), new Vector2(l.B.Y, l.B.X)))];
            LineSegment[] NonIntersectingVertical = [.. NonIntersectingLines.Select(l => new LineSegment(new Vector2(l.A.Y, l.A.X), new Vector2(l.B.Y, l.B.X)))];

            foreach (LineSegment other in IntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out Vector2 intersection);
                Assert.IsTrue(result);
            }

            foreach (LineSegment other in NonIntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out Vector2 intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineSegmentInParallelIntersects2()
        {
            //
            // TODO: Add test logic	here
            //

            LineSegment lineA = new(new Vector2(0, 10),
                                                        new Vector2(0, -10));
            LineSegment lineB = new(new Vector2(0, 11),  //Total overlap, beyond both endpoints
                                                        new Vector2(0, -11));
            LineSegment lineC = new(new Vector2(0, 3),  //Overlap, but not entirely
                                                        new Vector2(0, 15));
            LineSegment lineD = new(new Vector2(0, 10),  //Endpoint Overlaps
                                                        new Vector2(0, 15));
            LineSegment lineE = new(new Vector2(0, -10),    //Endpoint Overlaps
                                                        new Vector2(0, -15));
            LineSegment lineF = new(new Vector2(1, 10), //Parrallel, but slightly right
                                                        new Vector2(1, -10));
            LineSegment lineG = new(new Vector2(-1, 10), //Parallel, but slightly left
                                                        new Vector2(-1, -10));


            LineSegment[] IntersectingLines = [lineB, lineC, lineD, lineE];
            LineSegment[] NonIntersectingLines = [lineF, lineG];

            foreach (LineSegment other in IntersectingLines)
            {
                bool result = lineA.Intersects(other, out IShape2D intersection);
                Assert.IsTrue(result);
            }

            foreach (LineSegment other in NonIntersectingLines)
            {
                bool result = lineA.Intersects(other, out Vector2 intersection);
                Assert.IsFalse(result);
            }

            LineSegment vertLine = new(new Vector2(lineA.A.Y, lineA.A.X), new Vector2(lineA.B.Y, lineA.B.X));

            LineSegment[] IntersectingVertical = [.. IntersectingLines.Select(l => new LineSegment(new Vector2(l.A.Y, l.A.X), new Vector2(l.B.Y, l.B.X)))];
            LineSegment[] NonIntersectingVertical = [.. NonIntersectingLines.Select(l => new LineSegment(new Vector2(l.A.Y, l.A.X), new Vector2(l.B.Y, l.B.X)))];

            foreach (LineSegment other in IntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out Vector2 intersection);
                Assert.IsTrue(result);
            }

            foreach (LineSegment other in NonIntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out Vector2 intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineSegmentInParallelIntersects3()
        {
            //
            // TODO: Add test logic	here
            //

            LineSegment lineA = new(new Vector2(0, 0),
                                                        new Vector2(10, 10));
            LineSegment lineB = new(new Vector2(-1, -1),  //Total overlap, beyond both endpoints
                                                        new Vector2(11, 11));
            LineSegment lineC = new(new Vector2(3, 3),  //Overlap, but not entirely
                                                        new Vector2(15, 15));
            LineSegment lineD = new(new Vector2(10, 10),  //Endpoint Overlaps
                                                        new Vector2(15, 15));
            LineSegment lineE = new(new Vector2(-10, -10),    //Endpoint Overlaps
                                                        new Vector2(0, 0));
            LineSegment lineF = new(new Vector2(0, -1), //Parrallel, but slightly right
                                                        new Vector2(10, 9));
            LineSegment lineG = new(new Vector2(0, 1), //Parallel, but slightly left
                                                        new Vector2(10, 11));


            LineSegment[] IntersectingLines = [lineB, lineC, lineD, lineE];
            LineSegment[] NonIntersectingLines = [lineF, lineG];

            foreach (LineSegment other in IntersectingLines)
            {
                bool result = lineA.Intersects(other, out IShape2D intersection);
                Assert.IsTrue(result);
            }

            foreach (LineSegment other in NonIntersectingLines)
            {
                bool result = lineA.Intersects(other, out Vector2 intersection);
                Assert.IsFalse(result);
            }

            LineSegment vertLine = new(new Vector2(lineA.A.Y, lineA.A.X), new Vector2(lineA.B.Y, lineA.B.X));

            LineSegment[] IntersectingVertical = [.. IntersectingLines.Select(l => new LineSegment(new Vector2(l.A.Y, l.A.X), new Vector2(l.B.Y, l.B.X)))];
            LineSegment[] NonIntersectingVertical = [.. NonIntersectingLines.Select(l => new LineSegment(new Vector2(l.A.Y, l.A.X), new Vector2(l.B.Y, l.B.X)))];

            foreach (LineSegment other in IntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out Vector2 intersection);
                Assert.IsTrue(result);
            }

            foreach (LineSegment other in NonIntersectingVertical)
            {
                bool result = vertLine.Intersects(other, out Vector2 intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineIntersects()
        {
            //
            // TODO: Add test logic	here
            //

            Line lineA = new(new Vector2(-5, 0),
                                                        new Vector2(-10, 0));
            Line lineB = new(new Vector2(0, 5),
                                                        new Vector2(0, -5));

            Vector2 intersect = new();
            bool result = lineA.Intersects(lineB, out intersect);
            Assert.AreEqual(true, result);
            Assert.IsTrue(intersect.X == 0 && intersect.Y == 0);
        }

        [TestMethod]
        public void LineSetIntersectionsTest()
        {
            //Create a line mostly along the X axis.  Split it at x=2.5 and x=7.5.  Ensure we get three line segments and two intersection points
            Vector2 A = new(0, 0);
            Vector2 B = new(10, 1);

            LineSegment line = new(A, B);

            LineSegment[] OtherLines = [ new(new Vector2(2.5, 0), new Vector2(2.5, 10)),
                                                                   new(new Vector2(0, 11), new Vector2(10,11)), //A line that doesn't intersect
                                                                   new(new Vector2(7.5, 0), new Vector2(7.5, 10)) ];

            List<LineSegment> intersectingLines = line.Intersections(OtherLines, out Vector2[] splitPoints);

            Vector2 ExpectedIntersectionA = new(2.5, 0.25);
            Vector2 ExpectedIntersectionB = new(7.5, 0.75);

            Assert.AreEqual(2, splitPoints.Length);
            Assert.AreEqual(ExpectedIntersectionA, splitPoints[0]);
            Assert.AreEqual(ExpectedIntersectionB, splitPoints[1]);

            /*
            LineSegment[] expectedLines = new LineSegment[] { new LineSegment(A, ExpectedIntersectionA),
                                                                           new LineSegment(ExpectedIntersectionA, ExpectedIntersectionB),
                                                                           new LineSegment(ExpectedIntersectionB, B) };
                                                                           */
            LineSegment[] expectedLines = [OtherLines[0], OtherLines[2]];

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
            Vector2 A = new(0, 0);
            Vector2 B = new(10, 1);

            LineSegment line = new(A, B);

            LineSegment[] OtherLines = [ new(new Vector2(2.5, 0), new Vector2(2.5, 10)),
                                                                   new(new Vector2(0, 11), new Vector2(10,11)), //A line that doesn't intersect
                                                                   new(new Vector2(7.5, 0), new Vector2(7.5, 10)) ];

            List<LineSegment> dividedLines = line.SubdivideAtIntersections(OtherLines, out Vector2[] splitPoints);

            Vector2 ExpectedIntersectionA = new(2.5, 0.25);
            Vector2 ExpectedIntersectionB = new(7.5, 0.75);

            Assert.AreEqual(2, splitPoints.Length);
            Assert.AreEqual(ExpectedIntersectionA, splitPoints[0]);
            Assert.AreEqual(ExpectedIntersectionB, splitPoints[1]);


            LineSegment[] expectedLines = [ new(A, ExpectedIntersectionA),
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
            Vector2 A = new(0, 0);
            Vector2 B = new(10, 1);

            LineSegment line = new(A, B);

            LineSegment[] OtherLines = [ new(new Vector2(0, -1), new Vector2(0, 10)),
                                                                   new(new Vector2(0, 11), new Vector2(10,11)), //A line that doesn't intersect
                                                                   new(new Vector2(10, 0), new Vector2(10, 10)) ];

            List<LineSegment> dividedLines = line.SubdivideAtIntersections(OtherLines, out Vector2[] splitPoints);

            Assert.AreEqual(0, splitPoints.Length);

            LineSegment[] expectedLines = [new(A, B)];

            Assert.AreEqual(1, dividedLines.Count);

            for (int i = 0; i < dividedLines.Count; i++)
            {
                Assert.AreEqual(dividedLines[i], expectedLines[i]);
            }
        }

        /*
        public void TestSubdivideWithFSCheck()
        {
            Func<double, LineSegment, bool> subdivide_check = (val, line) =>
            {
                Vector2 linePoint = line.PointAlongLine(val);

            };
        }*/

        [TestMethod]
        public void TestIsLeft()
        {
            //Is a point to the left when standing at A looking at B
            Vector2 A = new(0, 0);
            Vector2 B = new(10, 0);
            LineSegment line = new(A, B);

            Vector2 left = new(0, 1);
            Vector2 right = new(0, -1);
            Vector2 on = A;

            Assert.AreEqual(1, line.IsLeft(left));
            Assert.AreEqual(-1, line.IsLeft(right));
            Assert.AreEqual(0, line.IsLeft(on));

            left = new Vector2(-1, 1);
            right = new Vector2(-1, -1);
            on = new Vector2(5, 0);

            Assert.AreEqual(1, line.IsLeft(left));
            Assert.AreEqual(-1, line.IsLeft(right));
            Assert.AreEqual(0, line.IsLeft(on));

            left = new Vector2(11, 1);
            right = new Vector2(11, -1);
            on = new Vector2(11, 0);

            Assert.AreEqual(1, line.IsLeft(left));
            Assert.AreEqual(-1, line.IsLeft(right));
            Assert.AreEqual(0, line.IsLeft(on));

            on = new Vector2(-1, 0);
            Assert.AreEqual(0, line.IsLeft(on));
        }

        [TestMethod]
        public void TestIsLeftWithFSCheck()
        {
            Arb.Register<Vector2Generators>();

            bool IsLeftCheck(Vector2 p, Vector2 q, Vector2 r)
            {
                if (p == q || q == r || r == p)
                    return true;

                LineSegment pq = new(p, q);
                LineSegment pr = new(p, r);

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

            Prop.ForAll<Vector2, Vector2, Vector2>(IsLeftCheck).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void TestIsLeftWithFSCheckOnHorizontalLine()
        {
            Arb.Register<Vector2Generators>();

            bool IsLeftCheck(Vector2 p)
            {
                LineSegment qr = new(new Vector2(-10, 0), new Vector2(10, 0));
                LineSegment rq = new(new Vector2(10, 0), new Vector2(-10, 0));

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

            Prop.ForAll<Vector2>(IsLeftCheck).QuickCheckThrowOnFailure();
        }

    }
}
