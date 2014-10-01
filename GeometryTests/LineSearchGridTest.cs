using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using Geometry; 

namespace GeometryTests
{
    /// <summary>
    /// Summary description for LineSearchGridTest
    /// </summary>
    [TestClass]
    public class LineSearchGridTest
    {
        public LineSearchGridTest()
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
        public void LineSearchGridTestMethod()
        {
            LineSearchGrid<string> LineSearch = new LineSearchGrid<string>(new GridRectangle(-10, 10, -10, 10), 500);

            GridLineSegment lineA = new GridLineSegment(new GridVector2(-5, 3),
                                                        new GridVector2(5, 3));
            GridLineSegment lineB = new GridLineSegment(new GridVector2(3, -5),
                                                        new GridVector2(3, 5));
            GridLineSegment lineC = new GridLineSegment(new GridVector2(-6, -5),
                                                        new GridVector2(-6, 5));
            GridLineSegment lineD = new GridLineSegment(new GridVector2(-9, 8),
                                                        new GridVector2(1, -8)); //Should be in seven grid cells
            GridLineSegment lineE = new GridLineSegment(new GridVector2(-9, 8),
                                                        new GridVector2(1, -2));

            LineSearch.Add(lineA, "A");
            LineSearch.Add(lineB, "B");
            LineSearch.Add(lineC, "C");
            LineSearch.Add(lineD, "D");
            LineSearch.Add(lineE, "E");
 
            GridVector2 intersection;
            double distance;
            string value = LineSearch.GetNearest(new GridVector2(-5, 3), out intersection, out distance);
            Debug.Assert(value == "A");

            value = LineSearch.GetNearest(new GridVector2(-10, -10), out intersection, out distance);
            Debug.Assert(value == "C");

            value = LineSearch.GetNearest(new GridVector2(7, 4), out intersection, out distance);
            Debug.Assert(value == "A");

            value = LineSearch.GetNearest(new GridVector2(3.5, 6), out intersection, out distance);
            Debug.Assert(value == "B"); 
        }
    }
}
