using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

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
            GridVector2[] points = [new(5, 0),
                                                        new(0, 5),
                                                        new(-5,0)];

            GridCircle circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(circle.Center.X == 0.0 && circle.Center.Y == 0.0);
            Assert.AreEqual(5.0, circle.Radius);

            points = [new(0,-5),
                                                        new(0, 5),
                                                        new(Math.Cos(-0.5) * 5, Math.Sin(-0.5) * 5)];


            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(0, 0)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);

            points = [new(5,0),
                                                        new(10, 5),
                                                        new(5, 10)];


            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(5, 5)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);


            points = [new(5,0),
                                                        new(5, 10),
                                                        new(10, 5)];


            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(5, 5)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);

            points = [new(Math.Cos(0.5) * 5, Math.Sin(0.5) * 5),
                                                        new(5, 0),
                                                        new(Math.Cos(-0.5) * 5, Math.Sin(-0.5) * 5)];

            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(0, 0)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);

            points = [new((Math.Cos(0.5) * 5) + 5, (Math.Sin(0.5) * 5) + 5),
                                                        new(10, 5),
                                                        new((Math.Cos(-0.5) * 5)+5, (Math.Sin(-0.5) * 5)+5)];

            circle = Geometry.GridCircle.CircleFromThreePoints(points);
            Assert.IsTrue(GridVector2.Distance(circle.Center, new GridVector2(5, 5)) < Geometry.Global.Epsilon);
            Assert.IsTrue(circle.Radius > 5.0 - Geometry.Global.Epsilon && circle.Radius < 5.0 + Geometry.Global.Epsilon);
        }
    }
}
