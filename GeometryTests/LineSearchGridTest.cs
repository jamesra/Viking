using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        public void LineSearchGridTestMethod()
        {
            LineSearchGrid<string> LineSearch = new(new Rectangle(-10, 10, -10, 10), 500);

            LineSegment lineA = new(new Vector2(-5, 3),
                                                        new Vector2(5, 3));
            LineSegment lineB = new(new Vector2(3, -5),
                                                        new Vector2(3, 5));
            LineSegment lineC = new(new Vector2(-6, -5),
                                                        new Vector2(-6, 5));
            LineSegment lineD = new(new Vector2(-9, 8),
                                                        new Vector2(1, -8)); //Should be in seven grid cells
            LineSegment lineE = new(new Vector2(-9, 8),
                                                        new Vector2(1, -2));

            LineSearch.Add(lineA, "A");
            LineSearch.Add(lineB, "B");
            LineSearch.Add(lineC, "C");
            LineSearch.Add(lineD, "D");
            LineSearch.Add(lineE, "E");

            string value = LineSearch.GetNearest(new Vector2(-5, 3), out Vector2 intersection, out double distance);
            Assert.AreEqual("A", value);

            value = LineSearch.GetNearest(new Vector2(-10, -10), out intersection, out distance);
            Assert.AreEqual("C", value);

            value = LineSearch.GetNearest(new Vector2(7, 4), out intersection, out distance);
            Assert.AreEqual("A", value);

            value = LineSearch.GetNearest(new Vector2(3.5, 6), out intersection, out distance);
            Assert.AreEqual("B", value);
        }
    }
}
