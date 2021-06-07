using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for GridPolylineTest
    /// </summary>
    [TestClass]
    public class GridPolylineTest
    {
        public GridPolylineTest()
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
        public void TestGenerator()
        {
            GeometryArbitraries.Register();

            Prop.ForAll<GridPolyline>((pl) =>
            {
                Trace.WriteLine(pl);
                bool NoSelfIntersection = pl.LineSegments.SelfIntersects(LineSetOrdering.POLYLINE) == false;
                bool OpenShape = pl.PointCount >= 2 ? pl.Points[0] != pl.Points[pl.Points.Count - 1] : true;
                bool IsLine = pl.PointCount >= 2;
                bool pass = NoSelfIntersection && OpenShape;
                return pass.Classify(NoSelfIntersection == false, "Self intersection")
                           .Classify(OpenShape == false, "Closed shape")
                           .Trivial(false == IsLine);
            }).QuickCheckThrowOnFailure();
            //
            // TODO: Add test logic here
            //
        }
    }
}
