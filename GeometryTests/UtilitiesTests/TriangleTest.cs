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
            Debug.Assert(tri.Intersects(outsidetest) == false);

            GridVector2 insidetest = new GridVector2(25, 75);
            Debug.Assert(tri.Intersects(insidetest) == true);
            
            //Bug Fix #1
            v1 = new GridVector2(6313.066666666, 13608);
            v2 = new GridVector2(4509.33, 12700.8);
            v3 = new GridVector2(2705.6, 11793.6);

            tri = new GridTriangle(v1, v2, v3);

            outsidetest = new GridVector2(double.MaxValue / 2, 10652.94);
            Debug.Assert(tri.Intersects(outsidetest) == false);
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


            int[] iTriangles = Delaunay.Triangulate(points);
            int[] iExpected = new int[] {0,1,4,0,3,4,1,2,5,1,4,5}; 

            Debug.WriteLine(iTriangles.ToString());
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


            iTriangles = Delaunay.Triangulate(points);
            iExpected = new int[] {3,4,7,3,6,7,4,5,8,4,7,8,0,1,4,0,3,4,1,2,5,1,4,5}; 

            Debug.WriteLine(iTriangles.ToString());
            Debug.Assert(iTriangles.Length / 3 == 8); //We should find four triangles

            for (int i = 0; i < iExpected.Length; i++)
            {
                Debug.Assert(iExpected[i] == iTriangles[i]);
            }
                         
        }
    }
}
