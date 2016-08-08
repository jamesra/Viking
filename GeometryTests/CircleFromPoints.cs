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
    /// Summary description for CircleFromPoints
    /// </summary>
    [TestClass]
    public class CircleTests
    {
        public CircleTests()
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
        public void TestCircleFromPoints()
        {
            //
            // TODO: Add test logic	here
            //
            GridVector2[] points = new GridVector2[]  {new GridVector2(5, 0),
                                                        new GridVector2(0, 5), 
                                                        new GridVector2(-5,0)};

            GridCircle circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(circle.Center.X == 0.0 && circle.Center.Y == 0.0);
            Assert.IsTrue(circle.Radius == 5.0);

            points = new GridVector2[]  {new GridVector2(0,-5),
                                                        new GridVector2(0, 5), 
                                                        new GridVector2(Math.Cos(-0.5) * 5, Math.Sin(-0.5) * 5)};


            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(0, 0)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);

            points = new GridVector2[]  {new GridVector2(5,0),
                                                        new GridVector2(10, 5), 
                                                        new GridVector2(5, 10)};


            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(5, 5)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);


            points = new GridVector2[]  {new GridVector2(5,0),
                                                        new GridVector2(5, 10), 
                                                        new GridVector2(10, 5)};


            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(5, 5)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);

            points = new GridVector2[]  {new GridVector2(Math.Cos(0.5) * 5, Math.Sin(0.5) * 5),
                                                        new GridVector2(5, 0), 
                                                        new GridVector2(Math.Cos(-0.5) * 5, Math.Sin(-0.5) * 5)};

            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(0,0)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);

            points = new GridVector2[]  {new GridVector2((Math.Cos(0.5) * 5) + 5, (Math.Sin(0.5) * 5) + 5),
                                                        new GridVector2(10, 5), 
                                                        new GridVector2((Math.Cos(-0.5) * 5)+5, (Math.Sin(-0.5) * 5)+5)};

            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(5, 5)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);
        }
    }
}
