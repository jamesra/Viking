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
        public void TestTriangleContains()
        {
            GridVector2 v1 = new GridVector2(50,50); 
            GridVector2 v2 = new GridVector2(15,50);
            GridVector2 v3 = new GridVector2(15,100);
            GridTriangle tri = new GridTriangle(v1, v2, v3);
            
            GridVector2 outsidetest = new GridVector2(5, 75);
            Debug.Assert(tri.Contains(outsidetest) == false);

            GridVector2 insidetest = new GridVector2(25, 75);
            Debug.Assert(tri.Contains(insidetest) == true);
            
            //Bug Fix #1
            v1 = new GridVector2(6313.066666666, 13608);
            v2 = new GridVector2(4509.33, 12700.8);
            v3 = new GridVector2(2705.6, 11793.6);

            tri = new GridTriangle(v1, v2, v3);

            outsidetest = new GridVector2(double.MaxValue / 2, 10652.94);
            Debug.Assert(tri.Contains(outsidetest) == false);
        }

        /// <summary>
        /// Ensure internal angles of the triangle have the expected values
        /// </summary>
        /// <param name="tri"></param>
        /// <param name="expected">Expected angles in degrees, sorted smallest to largest.  Rounded to nearest integer</param>
        private void VerifyInternalAngles(GridTriangle tri, double[] expected)
        {
            double[] angleDegrees = tri.Angles.Select(a => Math.Round(RadianToDegrees(a))).OrderBy(a => a).ToArray();

            Assert.AreEqual(angleDegrees[0], expected[0]);
            Assert.AreEqual(angleDegrees[1], expected[1]);
            Assert.AreEqual(angleDegrees[2], expected[2]);

            Assert.AreEqual(angleDegrees.Sum(), 180); 
        }

        [TestMethod]
        public void TestTriangleAngles()
        {
            GridVector2 v1 = new GridVector2(0, 0);
            GridVector2 v2 = new GridVector2(0, 10);
            GridVector2 v3 = new GridVector2(10, 0);
            GridTriangle tri = new GridTriangle(v1, v2, v3);

            double[] angleDegrees = tri.Angles.Select(a => (a / (2 * Math.PI)) * 360).OrderBy(a => a).ToArray();
            VerifyInternalAngles(tri, new double[] { 45, 45, 90 });

            v1 = new GridVector2(0, 0);
            v2 = new GridVector2(10, 0);
            v3 = new GridVector2(5, 10 * Math.Sin(DegreesToRadians(60)));
            double distance = GridVector2.Distance(v1, v3);
            Assert.AreEqual(distance, 10);
            tri = new GridTriangle(v1, v2, v3);
            VerifyInternalAngles(tri, new double[] { 60, 60, 60 });

            v1 = new GridVector2(0, 0);
            v2 = new GridVector2(Math.Sqrt(3), 0);
            v3 = new GridVector2(0, 1); 
            tri = new GridTriangle(v1, v2, v3);
            VerifyInternalAngles(tri, new double[] { 30, 60, 90 });

            v2 = new GridVector2(1, 0);

            for(int i = 1; i < 360; i++)
            {
                if (i == 180)
                    continue; 

                double radians = DegreesToRadians(i);
                v3 = new GridVector2(Math.Cos(radians), Math.Sin(radians));

                tri = new GridTriangle(v1, v2, v3);
                
                double adjustedAngle = radians > Math.PI ? 2 * Math.PI - radians : radians;
                //The remaining two angles should be equal 
                double expectedEqualAngles = (Math.PI - adjustedAngle) / 2.0;

                adjustedAngle = Math.Round(adjustedAngle, 5);
                expectedEqualAngles = Math.Round(expectedEqualAngles, 5);
                double[] angles = tri.Angles.Select(a => Math.Round(a,5)).ToArray();

                Assert.IsTrue(angles.Contains(adjustedAngle));
                Assert.IsTrue(angles.Where(a => a == expectedEqualAngles).Count() >= 2);
            }
        }

        private double RadianToDegrees(double radians)
        {
            return (radians / (Math.PI * 2.0)) * 360.0;
        }

        private double DegreesToRadians(double degrees)
        {
            return (degrees / 180.0) * Math.PI;
        }

        [TestMethod]
        public void TestDelaunay()
        {
            GridVector2[] points = new GridVector2[]{ new GridVector2(50, 50),
                                                      new GridVector2(50, 100),
                                                      new GridVector2(50, 150),
                                                       new GridVector2(150, 50),
                                                      new GridVector2(150, 100),
                                                      new GridVector2(150, 150)};


            int[] iTriangles = Delaunay2D.Triangulate(points);
            int[] iExpected = new int[] {0,1,4,0,3,4,1,2,5,1,4,5};

            Trace.WriteLine(iTriangles.ToString(), "Geometry");
            Debug.Assert(iTriangles.Length / 3 == 4); //We should find four triangles
            for(int i = 0; i < iExpected.Length; i++)
            {
                Debug.Assert(iExpected[i] == iTriangles[i]); 
            }

            points = new GridVector2[]{ new GridVector2(50, 50),
                                                      new GridVector2(50, 100),
                                                      new GridVector2(50, 150),
                                                      new GridVector2(150, 50),
                                                      new GridVector2(150, 100),
                                                      new GridVector2(150, 150),
                                                      new GridVector2(250, 50),
                                                      new GridVector2(250, 100),
                                                      new GridVector2(250, 150)};


            iTriangles = Delaunay2D.Triangulate(points);
            iExpected = new int[] {3,4,7,3,6,7,4,5,8,4,7,8,0,1,4,0,3,4,1,2,5,1,4,5};

            Trace.WriteLine(iTriangles.ToString(), "Geometry");
            Debug.Assert(iTriangles.Length / 3 == 8); //We should find four triangles

            for (int i = 0; i < iExpected.Length; i++)
            {
                Debug.Assert(iExpected[i] == iTriangles[i]);
            }
                         
        }
    }
}
