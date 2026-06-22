using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for TriangleTest
    /// </summary>
    [TestClass]
    public class TriangleTest
    {
        public TriangleTest()
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
        public void TestTriangleContains()
        {
            GridVector2 v1 = new(50, 50);
            GridVector2 v2 = new(15, 50);
            GridVector2 v3 = new(15, 100);
            GridTriangle tri = new(v1, v2, v3);

            GridVector2 outsidetest = new(5, 75);
            Assert.IsFalse(tri.Contains(outsidetest));

            GridVector2 insidetest = new(25, 75);
            Assert.IsTrue(tri.Contains(insidetest));

            //Bug Fix #1
            v1 = new GridVector2(6313.066666666, 13608);
            v2 = new GridVector2(4509.33, 12700.8);
            v3 = new GridVector2(2705.6, 11793.6);

            tri = new GridTriangle(v1, v2, v3);

            outsidetest = new GridVector2(double.MaxValue / 2, 10652.94);
            Assert.IsFalse(tri.Contains(outsidetest));
        }

        /// <summary>
        /// Ensure internal angles of the triangle have the expected values
        /// </summary>
        /// <param name="tri"></param>
        /// <param name="expected">Expected angles in degrees, sorted smallest to largest.  Rounded to nearest integer</param>
        private void VerifyInternalAngles(GridTriangle tri, double[] expected)
        {
            double[] angleDegrees = [.. tri.Angles.Select(a => Math.Round(TriangleTest.RadianToDegrees(a))).OrderBy(a => a)];

            Assert.AreEqual(angleDegrees[0], expected[0]);
            Assert.AreEqual(angleDegrees[1], expected[1]);
            Assert.AreEqual(angleDegrees[2], expected[2]);

            Assert.AreEqual(180, angleDegrees.Sum());
        }

        [TestMethod]
        public void TestTriangleAngles()
        {
            GridVector2 v1 = new(0, 0);
            GridVector2 v2 = new(0, 10);
            GridVector2 v3 = new(10, 0);
            GridTriangle tri = new(v1, v2, v3);

            double[] angleDegrees = [.. tri.Angles.Select(a => (a / (2 * Math.PI)) * 360).OrderBy(a => a)];
            VerifyInternalAngles(tri, [45, 45, 90]);

            v1 = new GridVector2(0, 0);
            v2 = new GridVector2(10, 0);
            v3 = new GridVector2(5, 10 * Math.Sin(TriangleTest.DegreesToRadians(60)));
            double distance = GridVector2.Distance(v1, v3);
            Assert.AreEqual(10, distance);
            tri = new GridTriangle(v1, v2, v3);
            VerifyInternalAngles(tri, [60, 60, 60]);

            v1 = new GridVector2(0, 0);
            v2 = new GridVector2(Math.Sqrt(3), 0);
            v3 = new GridVector2(0, 1);
            tri = new GridTriangle(v1, v2, v3);
            VerifyInternalAngles(tri, [30, 60, 90]);

            v2 = new GridVector2(1, 0);

            for (int i = 1; i < 360; i++)
            {
                if (i == 180)
                    continue;

                double radians = TriangleTest.DegreesToRadians(i);
                v3 = new GridVector2(Math.Cos(radians), Math.Sin(radians));

                tri = new GridTriangle(v1, v2, v3);

                double adjustedAngle = radians > Math.PI ? 2 * Math.PI - radians : radians;
                //The remaining two angles should be equal 
                double expectedEqualAngles = (Math.PI - adjustedAngle) / 2.0;

                adjustedAngle = Math.Round(adjustedAngle, 5);
                expectedEqualAngles = Math.Round(expectedEqualAngles, 5);
                double[] angles = [.. tri.Angles.Select(a => Math.Round(a, 5))];

                Assert.IsTrue(angles.Contains(adjustedAngle));
                Assert.IsTrue(angles.Where(a => a == expectedEqualAngles).Count() >= 2);
            }
        }

        private static double RadianToDegrees(double radians) => (radians / (Math.PI * 2.0)) * 360.0;

        private static double DegreesToRadians(double degrees) => (degrees / 180.0) * Math.PI;

        [TestMethod]
        public void TestDelaunay()
        {
            GridVector2[] points = [ new(50, 50),
                                                      new(50, 100),
                                                      new(50, 150),
                                                       new(150, 50),
                                                      new(150, 100),
                                                      new(150, 150)];


            int[] iTriangles = Delaunay2D.Triangulate(points);
            int[] iExpected = [0, 1, 4, 0, 3, 4, 1, 2, 5, 1, 4, 5];

            Assert.AreEqual(4, iTriangles.Length / 3);
            CollectionAssert.AreEqual(iExpected, iTriangles);

            points = [ new(50, 50),
                                                      new(50, 100),
                                                      new(50, 150),
                                                      new(150, 50),
                                                      new(150, 100),
                                                      new(150, 150),
                                                      new(250, 50),
                                                      new(250, 100),
                                                      new(250, 150)];


            iTriangles = Delaunay2D.Triangulate(points);
            iExpected = [3, 4, 7, 3, 6, 7, 4, 5, 8, 4, 7, 8, 0, 1, 4, 0, 3, 4, 1, 2, 5, 1, 4, 5];

            Assert.AreEqual(8, iTriangles.Length / 3);
            CollectionAssert.AreEqual(iExpected, iTriangles);

        }
    }
}
