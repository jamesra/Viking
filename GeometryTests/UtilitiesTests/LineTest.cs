using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using Utilities; 

namespace UtilitiesTests
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
                Debug.Assert(Distance == Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5,2)));
                Debug.Assert(Intersection == new GridVector2(0,0));

                GridVector2 PointPastEdge = new GridVector2(-10, 0);
                Distance = lineC.DistanceToPoint(PointPastEdge, out Intersection);
                Debug.Assert(Distance == Math.Sqrt(Math.Pow(5, 2) + Math.Pow(5,2)));
                Debug.Assert(Intersection == new GridVector2(-5,-5));
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

            GridLineSegment lineA = new GridLineSegment(new GridVector2(-5,3),
                                                        new GridVector2(5,3)); 
            GridLineSegment lineB = new GridLineSegment(new GridVector2(3,-5),
                                                        new GridVector2(3,5));
            GridLineSegment lineC = new GridLineSegment(new GridVector2(-6, -5),
                                                        new GridVector2(-6, 5));
            GridLineSegment lineD = new GridLineSegment(new GridVector2(-9, 8),
                                                        new GridVector2(1, -8));
            GridLineSegment lineE = new GridLineSegment(new GridVector2(-9, 8),
                                                        new GridVector2(1, -2));


            GridVector2 intersect = new GridVector2(); 
            bool result = lineA.Intersects(lineB, out intersect);
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
    }
}
