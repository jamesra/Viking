using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for QuadTreeTest
    /// </summary>
    [TestClass]
    public class QuadTreeTest
    {
        public QuadTreeTest()
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
        public void QuadTreeTestOne()
        {
            GridVector2[] points = new GridVector2[] { new GridVector2(0,0),
                                                       new GridVector2(1,1),
                                                       new GridVector2(-10,-10),
                                                       new GridVector2(-7.5, 2.5),
                                                       new GridVector2(8.5, -1.5),
                                                       new GridVector2(3.5, -6.5),
                                                       new GridVector2(1.5, -8.5),
                                                       new GridVector2(10, 10)};
            int[] values = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            GridRectangle border = GridVector2.Border(points);
            QuadTree<int> tree = new QuadTree<int>(points, values, border);

            //Start with a basic test ensuring we can find all the existing points
            for (int i = 0; i < points.Length; i++)
            {
                double distance;
                int RetValue;
                RetValue = tree.FindNearest(points[i], out distance);

                Debug.Assert(RetValue == i);
                Debug.Assert(distance == 0);
            }

            //Check to see if we can find nearby points
            GridVector2[] nearpoints = new GridVector2[] { new GridVector2(.25,.25),
                                                       new GridVector2(.5,.51),
                                                       new GridVector2(-7.5,-7.5),
                                                       new GridVector2(-7.5, -1.5),
                                                       new GridVector2(8.5, -5.5),
                                                       new GridVector2(4.5, -7.75),
                                                       new GridVector2(1, -8.75),
                                                       new GridVector2(11, 11)}; //Out of original boundaries


            for (int i = 0; i < nearpoints.Length; i++)
            {
                double distance;
                int RetValue;
                RetValue = tree.FindNearest(nearpoints[i], out distance);

                Debug.Assert(RetValue == i);
                Debug.Assert(distance == GridVector2.Distance(points[i], nearpoints[i]));
            }

            //Check to see if we can return all points in a rectangle
            GridRectangle gridRect = new GridRectangle(0, 15, 0, 15);
            List<GridVector2> intersectPoints;
            List<int> intersectValues;
            tree.Intersect(gridRect, out intersectPoints, out intersectValues);
            Debug.Assert(intersectValues.Contains(0));
            Debug.Assert(intersectValues.Contains(1));
            Debug.Assert(intersectValues.Contains(7));

            Assert.AreEqual(false, intersectValues.Contains(2));
            Assert.AreEqual(false, intersectValues.Contains(3));
            Assert.AreEqual(false, intersectValues.Contains(4));
            Assert.AreEqual(false, intersectValues.Contains(5));
            Assert.AreEqual(false, intersectValues.Contains(6));

        }

        [TestMethod]
        public void QuadTreeTestTwo()
        {
            int numPoints = 1000;
            double BoundarySize = 1000;
            int seed = 0;
            System.Random RandGen = new System.Random(seed);

            QuadTree<int> Tree = new QuadTree<int>(new GridRectangle(-BoundarySize, BoundarySize, -BoundarySize, BoundarySize));

            GridVector2[] points = new GridVector2[numPoints];

            //Create the QuadTree
            for (int i = 0; i < numPoints; i++)
            {
                points[i] = new GridVector2(RandGen.NextDouble() * BoundarySize, RandGen.NextDouble() * BoundarySize);
                Tree.Add(points[i], i);
            }

            double distance;

            //Check to see we can find every item in the quad tree
            for (int i = 0; i < numPoints; i++)
            {

                int iFound = Tree.FindNearest(points[i], out distance);
                Assert.AreEqual(iFound, i, "Could not find previously inserted point");
            }

            //Remove half the points
            for (int i = 0; i < numPoints / 2; i++)
            {
                int Value;
                bool Success = Tree.TryRemove(i, out Value);
                Assert.IsTrue(Success, "Could not remove previously inserted point");

                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                int iFound = Tree.FindNearest(points[i], out distance);
                Assert.IsTrue(iFound > i, "Found previously deleted point");
            }

            //Look for the remaining points
            for (int i = numPoints / 2; i < numPoints; i++)
            {
                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                int iFound = Tree.FindNearest(points[i], out distance);
                Assert.AreEqual(iFound, i, "Could not find previously inserted point after deletes");
            }

            //Re-insert the removed points
            for (int i = 0; i < numPoints / 2; i++)
            {
                Tree.Add(points[i], i);

                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                int iFound = Tree.FindNearest(points[i], out distance);
                Assert.AreEqual(iFound, i, "Could not find newly inserted point after deletes");
            }

            //Look for the remaining points
            for (int i = numPoints / 2; i < numPoints; i++)
            {
                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                int iFound = Tree.FindNearest(points[i], out distance);
                Assert.AreEqual(iFound, i, "Could not find previously inserted point after delete and insert");
            }

            //Delete all the points
            for (int i = 0; i < numPoints; i++)
            {
                int Value;
                bool Success = Tree.TryRemove(i, out Value);
                Debug.Assert(Success, "Could not remove previously inserted point");

                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                if (i < numPoints - 1)
                {
                    int iFound = Tree.FindNearest(points[i], out distance);
                    Assert.IsTrue(iFound > i, "Found previously deleted point");
                }
            }

            //Insert some points into the empty tree to make sure we still can 
            for (int i = 0; i < numPoints; i++)
            {
                points[i] = new GridVector2(RandGen.NextDouble() * BoundarySize, RandGen.NextDouble() * BoundarySize);
                Tree.Add(points[i], i);
            }

            //Check to see we can find every item in the quad tree
            for (int i = 0; i < numPoints; i++)
            {

                int iFound = Tree.FindNearest(points[i], out distance);
                Assert.AreEqual(iFound, i, "Could not find previously inserted point");
            }

            List<DistanceToPoint<int>> foundPoints = Tree.FindNearestPoints(new GridVector2(BoundarySize * -2, BoundarySize * -2), Tree.Count * 2);
            Assert.AreEqual(Tree.Count, foundPoints.Count);

            foundPoints = Tree.FindNearestPoints(GridVector2.Zero, Tree.Count * 2);
            Assert.AreEqual(Tree.Count, foundPoints.Count);

            //The end 
        }

        [TestMethod]
        public void QuadTreeFsCheck()
        {
            GeometryArbitraries.Register();

            Configuration config = Configuration.QuickThrowOnFailure;
            config.StartSize = 128;
            config.MaxNbOfTest = 250;

            QuadTreeSpec spec = new QuadTreeSpec();
            spec.ToProperty().Check(config);

            /*
            Prop.ForAll<GridVector2[]>(points =>
            {
                
                QuadTree<int> qTree = new QuadTree<int>(points.BoundingBox());

                for (int i = 0; i < points.Length; i++)
                {
                    //qTree.Add()
                }
            }
            );
            */
        }
    }
}
