using FsCheck;
using Geometry;
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
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
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
                GridLineSegment lineA = new GridLineSegment(new GridVector2(-5, 3),
                                                            new GridVector2(5, 3));

                //Check edge conditions for a horizontal line
                GridVector2 PointOnLine = new GridVector2(2, 3);
                double Distance;
                GridVector2 Intersection;
                Distance = lineA.DistanceToPoint(PointOnLine, out Intersection);
                Debug.Assert(Distance == 0);
                Debug.Assert(Intersection == PointOnLine);

                //Check if we go past the line in X axis
                GridVector2 PointLeftOfLine = new GridVector2(-10, 3);
                GridVector2 PointRightOfLine = new GridVector2(10, 3);
                Distance = lineA.DistanceToPoint(PointLeftOfLine, out Intersection);
                Debug.Assert(Distance == 5);
                Debug.Assert(Intersection == lineA.A);

                Distance = lineA.DistanceToPoint(PointRightOfLine, out Intersection);
                Debug.Assert(Distance == 5);
                Debug.Assert(Intersection == lineA.B);

                //Check if we go above or below line
                GridVector2 PointAboveLine = new GridVector2(3, 8);
                GridVector2 PointBelowLine = new GridVector2(3, -2);
                Distance = lineA.DistanceToPoint(PointAboveLine, out Intersection);
                Debug.Assert(Distance == 5);
                Debug.Assert(Intersection == new GridVector2(3, 3));

                Distance = lineA.DistanceToPoint(PointBelowLine, out Intersection);
                Debug.Assert(Distance == 5);
                Debug.Assert(Intersection == new GridVector2(3, 3));
            }


            //Check edge conditions for a vertical line
            {
                GridLineSegment lineB = new GridLineSegment(new GridVector2(3, -5),
                                                               new GridVector2(3, 5));

                GridVector2 PointOnLine = new GridVector2(3, 2);
                double Distance;
                GridVector2 Intersection;
                Distance = lineB.DistanceToPoint(PointOnLine, out Intersection);
                Debug.Assert(Distance == 0);
                Debug.Assert(Intersection == PointOnLine);

                //Check if we go above or below line
                GridVector2 PointAboveLine = new GridVector2(3, 10);
                GridVector2 PointBelowLine = new GridVector2(3, -10);
                Distance = lineB.DistanceToPoint(PointAboveLine, out Intersection);
                Debug.Assert(Distance == 5);
                Debug.Assert(Intersection == lineB.B);

                Distance = lineB.DistanceToPoint(PointBelowLine, out Intersection);
                Debug.Assert(Distance == 5);
                Debug.Assert(Intersection == lineB.A);

                //Check if we go left or right of line
                GridVector2 PointLeftOfLine = new GridVector2(-2, 4);
                GridVector2 PointRightOfLine = new GridVector2(8, 4);
                Distance = lineB.DistanceToPoint(PointLeftOfLine, out Intersection);
                Debug.Assert(Distance == 5);
                Debug.Assert(Intersection == new GridVector2(3, 4));

                Distance = lineB.DistanceToPoint(PointRightOfLine, out Intersection);
                Debug.Assert(Distance == 5);
                Debug.Assert(Intersection == new GridVector2(3, 4));
            }

            {   //Check the diagonal line through the axis center
                GridLineSegment lineC = new GridLineSegment(new GridVector2(-5, -5),
                                                               new GridVector2(5, 5));

                GridVector2 PointOnLine = new GridVector2(0, 0);
                double Distance;
                GridVector2 Intersection;
                Distance = lineC.DistanceToPoint(PointOnLine, out Intersection);
                Debug.Assert(Distance == 0);
                Debug.Assert(Intersection == PointOnLine);

                GridVector2 PointOffLine = new GridVector2(-5, 5);
                Distance = lineC.DistanceToPoint(PointOffLine, out Intersection);
                Debug.Assert(Distance == Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)));
                Debug.Assert(Intersection == new GridVector2(0, 0));

                GridVector2 PointPastEdge = new GridVector2(-10, 0);
                Distance = lineC.DistanceToPoint(PointPastEdge, out Intersection);
                Debug.Assert(Distance == Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)));
                Debug.Assert(Intersection == new GridVector2(-5, -5));
            }

            {   //Check the diagonal line through the axis center
                GridLineSegment lineD = new GridLineSegment(new GridVector2(-6, -4),
                                                               new GridVector2(4, 6));

                GridVector2 PointOnLine = new GridVector2(-1, 1);
                double Distance;
                GridVector2 Intersection;
                Distance = lineD.DistanceToPoint(PointOnLine, out Intersection);
                Debug.Assert(Distance == 0);
                Debug.Assert(Intersection == PointOnLine);

                GridVector2 PointOffLine = new GridVector2(-6, 6);
                Distance = lineD.DistanceToPoint(PointOffLine, out Intersection);
                Debug.Assert(Distance == Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)));
                Debug.Assert(Intersection == new GridVector2(-1, 1));

                GridVector2 PointPastEdge = new GridVector2(9, 1);
                Distance = lineD.DistanceToPoint(PointPastEdge, out Intersection);
                Debug.Assert(Distance == Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5, 2)));
                Debug.Assert(Intersection == new GridVector2(4, 6));
            }
        }

        [TestMethod]
        public void GridLineSegmentIntersects()
        {
            //
            // TODO: Add test logic	here
            //

            GridLineSegment lineA = new GridLineSegment(new GridVector2(-5, 3),
                                                        new GridVector2(5, 3));
            GridLineSegment lineB = new GridLineSegment(new GridVector2(3, -5),
                                                        new GridVector2(3, 5));
            GridLineSegment lineC = new GridLineSegment(new GridVector2(-6, -5),
                                                        new GridVector2(-6, 5));
            GridLineSegment lineD = new GridLineSegment(new GridVector2(-9, 8),
                                                        new GridVector2(1, -8));
            GridLineSegment lineE = new GridLineSegment(new GridVector2(-9, 8),
                                                        new GridVector2(1, -2));

            IShape2D intersectShape;
            bool result = lineA.Intersects(lineA, out intersectShape);
            Debug.Assert(result == true);
            Debug.Assert(intersectShape.ShapeType == ShapeType2D.LINE);
            GridLineSegment intersectionLine = (GridLineSegment)intersectShape;
            Debug.Assert(intersectionLine == lineA);

            GridVector2 intersect;
            intersect = new GridVector2();
            result = lineA.Intersects(lineB, out intersect);
            Debug.Assert(result == true);
            Debug.Assert(intersect.X == 3 && intersect.Y == 3);

            result = lineA.Intersects(lineC, out intersect);
            Debug.Assert(result == false);

            result = lineA.Intersects(lineD, out intersect);
            Debug.Assert(result == false);
            //      Debug.Assert(intersect.X == -4 && intersect.Y == 3);

            result = lineA.Intersects(lineE, out intersect);
            Debug.Assert(result == true);
            Debug.Assert(intersect.X == -4 && intersect.Y == 3);
        }

        [TestMethod]
        public void GridLineSegmentInParallelIntersects()
        {
            //
            // TODO: Add test logic	here
            //

            GridLineSegment lineA = new GridLineSegment(new GridVector2(-5, 5),
                                                        new GridVector2(5, 5));
            GridLineSegment lineB = new GridLineSegment(new GridVector2(-7, 5),  //Total overlap, beyond both endpoints
                                                        new GridVector2(7, 5));
            GridLineSegment lineC = new GridLineSegment(new GridVector2(-3, 5),  //Overlap, but not entirely
                                                        new GridVector2(3, 5));
            GridLineSegment lineD = new GridLineSegment(new GridVector2(-10, 5),  //Endpoint Overlaps
                                                        new GridVector2(-5, 5));
            GridLineSegment lineE = new GridLineSegment(new GridVector2(5, 5),    //Endpoint Overlaps
                                                        new GridVector2(10, 5));
            GridLineSegment lineF = new GridLineSegment(new GridVector2(-5, 4), //Parrallel, but slightly above
                                                        new GridVector2(5, 4));
            GridLineSegment lineG = new GridLineSegment(new GridVector2(-5, 6), //Parallel, but slightly below
                                                        new GridVector2(5, 6));

            GridLineSegment[] IntersectingLines = new GridLineSegment[] { lineB, lineC, lineD, lineE };
            GridLineSegment[] NonIntersectingLines = new GridLineSegment[] { lineF, lineG };


            foreach (GridLineSegment other in IntersectingLines)
            {
                IShape2D intersection;
                bool result = lineA.Intersects(other, out intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingLines)
            {
                GridVector2 intersection;
                bool result = lineA.Intersects(other, out intersection);
                Assert.IsFalse(result);
            }

            GridLineSegment vertLine = new GridLineSegment(new GridVector2(lineA.A.Y, lineA.A.X), new GridVector2(lineA.B.Y, lineA.B.X));

            GridLineSegment[] IntersectingVertical = IntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X))).ToArray();
            GridLineSegment[] NonIntersectingVertical = NonIntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X))).ToArray();

            foreach (GridLineSegment other in IntersectingVertical)
            {
                GridVector2 intersection;
                bool result = vertLine.Intersects(other, out intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingVertical)
            {
                GridVector2 intersection;
                bool result = vertLine.Intersects(other, out intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineSegmentInParallelIntersects2()
        {
            //
            // TODO: Add test logic	here
            //

            GridLineSegment lineA = new GridLineSegment(new GridVector2(0, 10),
                                                        new GridVector2(0, -10));
            GridLineSegment lineB = new GridLineSegment(new GridVector2(0, 11),  //Total overlap, beyond both endpoints
                                                        new GridVector2(0, -11));
            GridLineSegment lineC = new GridLineSegment(new GridVector2(0, 3),  //Overlap, but not entirely
                                                        new GridVector2(0, 15));
            GridLineSegment lineD = new GridLineSegment(new GridVector2(0, 10),  //Endpoint Overlaps
                                                        new GridVector2(0, 15));
            GridLineSegment lineE = new GridLineSegment(new GridVector2(0, -10),    //Endpoint Overlaps
                                                        new GridVector2(0, -15));
            GridLineSegment lineF = new GridLineSegment(new GridVector2(1, 10), //Parrallel, but slightly right
                                                        new GridVector2(1, -10));
            GridLineSegment lineG = new GridLineSegment(new GridVector2(-1, 10), //Parallel, but slightly left
                                                        new GridVector2(-1, -10));


            GridLineSegment[] IntersectingLines = new GridLineSegment[] { lineB, lineC, lineD, lineE };
            GridLineSegment[] NonIntersectingLines = new GridLineSegment[] { lineF, lineG };

            foreach (GridLineSegment other in IntersectingLines)
            {
                IShape2D intersection;
                bool result = lineA.Intersects(other, out intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingLines)
            {
                GridVector2 intersection;
                bool result = lineA.Intersects(other, out intersection);
                Assert.IsFalse(result);
            }

            GridLineSegment vertLine = new GridLineSegment(new GridVector2(lineA.A.Y, lineA.A.X), new GridVector2(lineA.B.Y, lineA.B.X));

            GridLineSegment[] IntersectingVertical = IntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X))).ToArray();
            GridLineSegment[] NonIntersectingVertical = NonIntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X))).ToArray();

            foreach (GridLineSegment other in IntersectingVertical)
            {
                GridVector2 intersection;
                bool result = vertLine.Intersects(other, out intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingVertical)
            {
                GridVector2 intersection;
                bool result = vertLine.Intersects(other, out intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineSegmentInParallelIntersects3()
        {
            //
            // TODO: Add test logic	here
            //

            GridLineSegment lineA = new GridLineSegment(new GridVector2(0, 0),
                                                        new GridVector2(10, 10));
            GridLineSegment lineB = new GridLineSegment(new GridVector2(-1, -1),  //Total overlap, beyond both endpoints
                                                        new GridVector2(11, 11));
            GridLineSegment lineC = new GridLineSegment(new GridVector2(3, 3),  //Overlap, but not entirely
                                                        new GridVector2(15, 15));
            GridLineSegment lineD = new GridLineSegment(new GridVector2(10, 10),  //Endpoint Overlaps
                                                        new GridVector2(15, 15));
            GridLineSegment lineE = new GridLineSegment(new GridVector2(-10, -10),    //Endpoint Overlaps
                                                        new GridVector2(0, 0));
            GridLineSegment lineF = new GridLineSegment(new GridVector2(0, -1), //Parrallel, but slightly right
                                                        new GridVector2(10, 9));
            GridLineSegment lineG = new GridLineSegment(new GridVector2(0, 1), //Parallel, but slightly left
                                                        new GridVector2(10, 11));


            GridLineSegment[] IntersectingLines = new GridLineSegment[] { lineB, lineC, lineD, lineE };
            GridLineSegment[] NonIntersectingLines = new GridLineSegment[] { lineF, lineG };

            foreach (GridLineSegment other in IntersectingLines)
            {
                IShape2D intersection;
                bool result = lineA.Intersects(other, out intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingLines)
            {
                GridVector2 intersection;
                bool result = lineA.Intersects(other, out intersection);
                Assert.IsFalse(result);
            }

            GridLineSegment vertLine = new GridLineSegment(new GridVector2(lineA.A.Y, lineA.A.X), new GridVector2(lineA.B.Y, lineA.B.X));

            GridLineSegment[] IntersectingVertical = IntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X))).ToArray();
            GridLineSegment[] NonIntersectingVertical = NonIntersectingLines.Select(l => new GridLineSegment(new GridVector2(l.A.Y, l.A.X), new GridVector2(l.B.Y, l.B.X))).ToArray();

            foreach (GridLineSegment other in IntersectingVertical)
            {
                GridVector2 intersection;
                bool result = vertLine.Intersects(other, out intersection);
                Assert.IsTrue(result);
            }

            foreach (GridLineSegment other in NonIntersectingVertical)
            {
                GridVector2 intersection;
                bool result = vertLine.Intersects(other, out intersection);
                Assert.IsFalse(result);
            }
        }

        [TestMethod]
        public void GridLineIntersects()
        {
            //
            // TODO: Add test logic	here
            //

            GridLine lineA = new GridLine(new GridVector2(-5, 0),
                                                        new GridVector2(-10, 0));
            GridLine lineB = new GridLine(new GridVector2(0, 5),
                                                        new GridVector2(0, -5));

            GridVector2 intersect = new GridVector2();
            bool result = lineA.Intersects(lineB, out intersect);
            Debug.Assert(result == true);
            Debug.Assert(intersect.X == 0 && intersect.Y == 0);
        }

        [TestMethod]
        public void LineSetIntersectionsTest()
        {
            //Create a line mostly along the X axis.  Split it at x=2.5 and x=7.5.  Ensure we get three line segments and two intersection points
            GridVector2 A = new GridVector2(0, 0);
            GridVector2 B = new GridVector2(10, 1);

            GridLineSegment line = new GridLineSegment(A, B);

            GridLineSegment[] OtherLines = new GridLineSegment[] { new GridLineSegment(new GridVector2(2.5, 0), new GridVector2(2.5, 10)),
                                                                   new GridLineSegment(new GridVector2(0, 11), new GridVector2(10,11)), //A line that doesn't intersect
                                                                   new GridLineSegment(new GridVector2(7.5, 0), new GridVector2(7.5, 10)) };

            GridVector2[] splitPoints;
            List<GridLineSegment> intersectingLines = line.Intersections(OtherLines, out splitPoints);

            GridVector2 ExpectedIntersectionA = new GridVector2(2.5, 0.25);
            GridVector2 ExpectedIntersectionB = new GridVector2(7.5, 0.75);

            Assert.AreEqual(splitPoints.Length, 2);
            Assert.AreEqual(splitPoints[0], ExpectedIntersectionA);
            Assert.AreEqual(splitPoints[1], ExpectedIntersectionB);

            /*
            GridLineSegment[] expectedLines = new GridLineSegment[] { new GridLineSegment(A, ExpectedIntersectionA),
                                                                           new GridLineSegment(ExpectedIntersectionA, ExpectedIntersectionB),
                                                                           new GridLineSegment(ExpectedIntersectionB, B) };
                                                                           */
            GridLineSegment[] expectedLines = new GridLineSegment[] { OtherLines[0], OtherLines[2] };

            Assert.AreEqual(intersectingLines.Count, 2);

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
            GridVector2 A = new GridVector2(0, 0);
            GridVector2 B = new GridVector2(10, 1);

            GridLineSegment line = new GridLineSegment(A, B);

            GridLineSegment[] OtherLines = new GridLineSegment[] { new GridLineSegment(new GridVector2(2.5, 0), new GridVector2(2.5, 10)),
                                                                   new GridLineSegment(new GridVector2(0, 11), new GridVector2(10,11)), //A line that doesn't intersect
                                                                   new GridLineSegment(new GridVector2(7.5, 0), new GridVector2(7.5, 10)) };

            GridVector2[] splitPoints;
            List<GridLineSegment> dividedLines = line.SubdivideAtIntersections(OtherLines, out splitPoints);

            GridVector2 ExpectedIntersectionA = new GridVector2(2.5, 0.25);
            GridVector2 ExpectedIntersectionB = new GridVector2(7.5, 0.75);

            Assert.AreEqual(splitPoints.Length, 2);
            Assert.AreEqual(splitPoints[0], ExpectedIntersectionA);
            Assert.AreEqual(splitPoints[1], ExpectedIntersectionB);


            GridLineSegment[] expectedLines = new GridLineSegment[] { new GridLineSegment(A, ExpectedIntersectionA),
                                                                           new GridLineSegment(ExpectedIntersectionA, ExpectedIntersectionB),
                                                                           new GridLineSegment(ExpectedIntersectionB, B) };

            Assert.AreEqual(dividedLines.Count, 3);

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
            GridVector2 A = new GridVector2(0, 0);
            GridVector2 B = new GridVector2(10, 1);

            GridLineSegment line = new GridLineSegment(A, B);

            GridLineSegment[] OtherLines = new GridLineSegment[] { new GridLineSegment(new GridVector2(0, -1), new GridVector2(0, 10)),
                                                                   new GridLineSegment(new GridVector2(0, 11), new GridVector2(10,11)), //A line that doesn't intersect
                                                                   new GridLineSegment(new GridVector2(10, 0), new GridVector2(10, 10)) };

            GridVector2[] splitPoints;
            List<GridLineSegment> dividedLines = line.SubdivideAtIntersections(OtherLines, out splitPoints);

            Assert.AreEqual(splitPoints.Length, 0);

            GridLineSegment[] expectedLines = new GridLineSegment[] { new GridLineSegment(A, B) };

            Assert.AreEqual(dividedLines.Count, 1);

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
            GridVector2 A = new GridVector2(0, 0);
            GridVector2 B = new GridVector2(10, 0);
            GridLineSegment line = new GridLineSegment(A, B);

            GridVector2 left = new GridVector2(0, 1);
            GridVector2 right = new GridVector2(0, -1);
            GridVector2 on = A;

            Assert.AreEqual(line.IsLeft(left), 1);
            Assert.AreEqual(line.IsLeft(right), -1);
            Assert.AreEqual(line.IsLeft(on), 0);

            left = new GridVector2(-1, 1);
            right = new GridVector2(-1, -1);
            on = new GridVector2(5, 0);

            Assert.AreEqual(line.IsLeft(left), 1);
            Assert.AreEqual(line.IsLeft(right), -1);
            Assert.AreEqual(line.IsLeft(on), 0);

            left = new GridVector2(11, 1);
            right = new GridVector2(11, -1);
            on = new GridVector2(11, 0);

            Assert.AreEqual(line.IsLeft(left), 1);
            Assert.AreEqual(line.IsLeft(right), -1);
            Assert.AreEqual(line.IsLeft(on), 0);

            on = new GridVector2(-1, 0);
            Assert.AreEqual(line.IsLeft(on), 0);
        }

        [TestMethod]
        public void TestIsLeftWithFSCheck()
        {
            Arb.Register<GridVector2Generators>();

            Func<GridVector2, GridVector2, GridVector2, bool> IsLeftCheck = (p, q, r) =>
            {
                if (p == q || q == r || r == p)
                    return true;

                GridLineSegment pq = new GridLineSegment(p, q);
                GridLineSegment pr = new GridLineSegment(p, r);

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
            };

            Prop.ForAll<GridVector2, GridVector2, GridVector2>(IsLeftCheck).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void TestIsLeftWithFSCheckOnHorizontalLine()
        {
            Arb.Register<GridVector2Generators>();

            Func<GridVector2, bool> IsLeftCheck = (p) =>
            {
                GridLineSegment qr = new GridLineSegment(new GridVector2(-10, 0), new GridVector2(10, 0));
                GridLineSegment rq = new GridLineSegment(new GridVector2(10, 0), new GridVector2(-10, 0));

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
            };

            Prop.ForAll<GridVector2>(IsLeftCheck).QuickCheckThrowOnFailure();
        }

    }
}
