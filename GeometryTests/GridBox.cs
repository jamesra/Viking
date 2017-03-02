using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry;
using System.Diagnostics;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for GridRectangle
    /// </summary>
    [TestClass]
    public class GridBoxTest
    {
        public GridBoxTest()
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
        public void TestGridBoxIntersectsAndContains()
        {
            GridVector3 BotLeftA = new GridVector3(10, -50, 25);
            GridVector3 TopRightA = new GridVector3(20, -20, 50);
            GridBox boxA = new GridBox(BotLeftA, TopRightA);

            bool success = boxA.Contains(new GridVector3(10, -20, 25));
            Assert.IsTrue(success);

            success = boxA.Contains(new GridVector3(20, -50, 50));
            Assert.IsTrue(success);

            success = boxA.Contains(new GridVector3(15, -35, 35));
            Assert.IsTrue(success);

            success = boxA.Contains(boxA.Center);
            Assert.IsTrue(success);

            //--------------------------------------------------------------

            GridVector3 BotLeftB = new GridVector3(15, -40, 10);
            GridVector3 TopRightB = new GridVector3(25, 0, 26);
            GridBox boxBOverlaps = new GridBox(BotLeftB, TopRightB);
            success = boxA.Intersects(boxBOverlaps);
            Assert.IsTrue(success);

            success = boxBOverlaps.Intersects(boxA);
            Assert.IsTrue(success);

            //--------------------------------------------------------------

            GridVector3 BotLeftC = new GridVector3(0, -60, 20);
            GridVector3 TopRightC = new GridVector3(25, -10, 60);
            GridBox boxCContainsA = new GridBox(BotLeftC, TopRightC);
            success = boxCContainsA.Contains(boxA);
            Assert.IsTrue(success);

            success = boxA.Contains(boxCContainsA);
            Assert.IsFalse(success);

            //--------------------------------------------------------------

            GridVector3 BotLeftD = new GridVector3(-1000, 100, 50);
            GridVector3 TopRightD = new GridVector3(-900, 200, 60);
            GridBox boxDNoOverlap = new GridBox(BotLeftD, TopRightD);
            success = boxDNoOverlap.Contains(boxA);
            Assert.IsFalse(success);

            success = boxA.Contains(boxDNoOverlap);
            Assert.IsFalse(success);

            success = boxDNoOverlap.Intersects(boxA);
            Assert.IsFalse(success);

            success = boxA.Intersects(boxDNoOverlap);
            Assert.IsFalse(success);
        }

        [TestMethod]
        public void TestGridBoxScale()
        {
            double[] original_mins = new double[] { -20, -20, 50 };
            GridVector3 BotLeftA = new GridVector3(-20, -20, 50);
            GridVector3 TopRightA = new GridVector3(0, 20, 150);
            GridBox boxA = new GridBox(BotLeftA, TopRightA);

            //--------------------------------------------------------------
            Assert.IsTrue(boxA.minVals.Select((val, i) => (original_mins[i]) == val).All(b => b));

            /*Scale the rectangle and test again*/
            boxA.Scale(2);

            double[] scaled_mins = new double[] { -30, -40, 0 };
            double[] scaled_maxs = new double[] { 10, 40, 200 };
            Assert.IsTrue(boxA.minVals.Select((val, i) => (scaled_mins[i]) == val).All(b => b));
            Assert.IsTrue(boxA.maxVals.Select((val, i) => (scaled_maxs[i]) == val).All(b => b));

            /*Scale the rectangle and test again*/
            boxA.Scale(0.5);

            Assert.IsTrue(boxA.minVals.Select((val, i) => (original_mins[i]) == val).All(b => b));
        }
    }
}
