using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for Rectangle
    /// </summary>
    [TestClass]
    public class RectangleTest
    {
        public RectangleTest()
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
        public void TestGridRectangle()
        {
            Rectangle rectA = new(10, 50, 20, 40);

            bool success = rectA.Contains(new Vector2(10, 20));
            Assert.IsTrue(success);

            success = rectA.Contains(new Vector2(50, 40));
            Assert.IsTrue(success);

            success = rectA.Contains(rectA.Center);
            Assert.IsTrue(success);

            Rectangle rectBOverlaps = new(5, 15, 10, 21);
            success = rectA.Intersects(rectBOverlaps);
            Assert.IsTrue(success);

            success = rectA.Contains(rectBOverlaps);
            Assert.AreEqual(success, false);

            Rectangle rectCNoOverlap = new(5, 15, 10, 19);
            success = rectA.Intersects(rectCNoOverlap);
            Assert.AreEqual(success, false);

            success = rectA.Contains(rectBOverlaps);
            Assert.AreEqual(success, false);

            Rectangle rectDContained = new(15, 45, 25, 35);
            success = rectA.Intersects(rectDContained);
            Assert.IsTrue(success);

            success = rectA.Contains(rectDContained);
            Assert.IsTrue(success);

            /*Scale the rectangle and test again*/
            rectA *= 2;

            Assert.IsTrue(-10.0 == rectA.Left &&
                         70 == rectA.Right &&
                         10 == rectA.Bottom &&
                         50 == rectA.Top);

            /*Scale the rectangle and test again*/
            rectA *= 0.5;

            Assert.IsTrue(10.0 == rectA.Left &&
                         50 == rectA.Right &&
                         20 == rectA.Bottom &&
                         40 == rectA.Top);


        }


        [TestMethod]
        public void TestGridRectangleUnion()
        {
            Rectangle rectA = new(10, 20, 10, 30);
            Rectangle rectB = new(-20, -10, -15, -5);

            rectA += rectB;

            Assert.AreEqual(rectA, new Rectangle(-20, 20, -15, 30));

            rectB += rectA;

            Assert.AreEqual(rectB, new Rectangle(-20, 20, -15, 30));
        }
    }
}
