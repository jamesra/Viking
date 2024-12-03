using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;

namespace GeometryTests
{
    /// <summary>
    /// Summary description for QuadTreeWithUniqueValuesTest
    /// </summary>
    [TestClass]
    public class QuadTree
    {
        public QuadTree()
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
        public void QuadTreeTestSimpleAddRemove()
        {
            GridRectangle border = new GridRectangle(-10, 10, -10, 10);
            QuadTreeWithUniqueValues<int> tree = new QuadTreeWithUniqueValues<int>(border);
            Assert.IsTrue(tree.Count == 0);

            GridVector2 p = new GridVector2(0, 0);
            int value = 0;
            tree.Add(p, value);
            Assert.IsTrue(tree.Count == 1);
            Assert.IsTrue(tree.Contains(p));
            Assert.IsTrue(tree.Contains(0));
            Assert.IsTrue(tree[p] == value);


            bool removed = tree.TryRemove(0, out var found);
            Assert.IsTrue(removed);
            Assert.IsTrue(tree.Count == 0);
            Assert.IsTrue(found == value);
            Assert.IsFalse(tree.Contains(p));
            Assert.IsFalse(tree.Contains(0));
        }

        [TestMethod]
        public void QuadTreeTestSimpleAddRemoveUpdate()
        {
            GridRectangle border = new GridRectangle(-10, 10, -10, 10);
            QuadTreeWithUniqueValues<int> tree = new QuadTreeWithUniqueValues<int>(border);
            Assert.IsTrue(tree.Count == 0);
            GridVector2 p = new GridVector2(0, 0); 
            int value = 0;
            tree.Add(p, value);
            Assert.IsTrue(tree.Count == 1);
            Assert.IsTrue(tree.Contains(p));
            Assert.IsTrue(tree.Contains(0));
            Assert.IsTrue(tree[p] == value);

            int updated_value = 1;
            tree[p] = updated_value;
            Assert.IsTrue(tree[p] == updated_value);

            int missing_value = 2;
            GridVector2 missing_point = new GridVector2(1, 1);
            try { 
                tree[missing_point] = missing_value;
                Assert.Fail("Missing value should not be able to be updated");
            }
            catch (KeyNotFoundException)
            {
                //This is expected
            }

            try
            {
                tree.Update(missing_point, missing_value);
                Assert.Fail("Missing value should not be able to be updated");
            }
            catch (KeyNotFoundException)
            {
                //This is expected
            }

            int second_value = 3;
            GridVector2 second_p = new GridVector2(-1,0);

            tree.Add(second_p, second_value);

            bool removed = tree.TryRemove(updated_value, out var found);
            Assert.IsTrue(removed);
            Assert.IsTrue(tree.Count == 1);
            Assert.IsTrue(found == updated_value);
            Assert.IsFalse(tree.Contains(p));
            Assert.IsFalse(tree.Contains(updated_value));

            bool point_removed = tree.TryRemove(second_p, out var  removed_value);
            Assert.IsTrue(point_removed);
            Assert.AreEqual(removed_value, second_value);

            point_removed = tree.TryRemove(missing_point, out var removed_missing_value);
            Assert.IsFalse(point_removed);
        }

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
            QuadTreeWithUniqueValues<int> treeWithUniqueValues = new QuadTreeWithUniqueValues<int>(points, values, border);

            //Start with a basic test ensuring we can find all the existing points
            for (int i = 0; i < points.Length; i++)
            {
                bool found = treeWithUniqueValues.TryFindNearest(points[i], out var retValue, out double distance);

                Assert.IsTrue(found);
                Assert.IsTrue(retValue == i);
                Assert.IsTrue(distance == 0);
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
                bool found  = treeWithUniqueValues.TryFindNearest(nearpoints[i], out var retValue, out var distance);

                Assert.IsTrue(found);
                Assert.IsTrue(retValue == i);
                Assert.IsTrue(distance == GridVector2.Distance(points[i], nearpoints[i]));
            }

            //Check to see if we can return all points in a rectangle
            GridRectangle gridRect = new GridRectangle(0, 15, 0, 15);
            treeWithUniqueValues.Intersect(gridRect, out List<GridVector2> intersectPoints, out List<int> intersectValues);
            Assert.IsTrue(intersectValues.Contains(0));
            Assert.IsTrue(intersectValues.Contains(1));
            Assert.IsTrue(intersectValues.Contains(7));

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

            QuadTreeWithUniqueValues<int> treeWithUniqueValues = new QuadTreeWithUniqueValues<int>(new GridRectangle(-BoundarySize, BoundarySize, -BoundarySize, BoundarySize));

            GridVector2[] points = new GridVector2[numPoints];

            //Create the QuadTreeWithUniqueValues
            for (int i = 0; i < numPoints; i++)
            {
                points[i] = new GridVector2(RandGen.NextDouble() * BoundarySize, RandGen.NextDouble() * BoundarySize);
                treeWithUniqueValues.Add(points[i], i);
            }

            double distance;

            //Check to see we can find every item in the quad treeWithUniqueValues
            for (int i = 0; i < numPoints; i++)
            {

                bool found = treeWithUniqueValues.TryFindNearest(points[i], out var iFound, out distance);
                Assert.IsTrue(found);
                Assert.AreEqual(iFound, i, "Could not find previously inserted point");
            }

            //Remove half the points
            for (int i = 0; i < numPoints / 2; i++)
            {
                bool Success = treeWithUniqueValues.TryRemove(i, out int Value);
                Assert.IsTrue(Success, "Could not remove previously inserted point");

                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                bool found = treeWithUniqueValues.TryFindNearest(points[i], out var iFound, out distance);
                Assert.IsTrue(found);
                Assert.IsTrue(iFound > i, "Found previously deleted point");

                Assert.IsFalse(treeWithUniqueValues.Contains(i));
            }

            //Look for the remaining points
            for (int i = numPoints / 2; i < numPoints; i++)
            {
                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                bool found = treeWithUniqueValues.TryFindNearest(points[i], out var iFound, out distance);
                Assert.IsTrue(found);
                Assert.AreEqual(iFound, i, "Could not find previously inserted point after deletes");
            }

            //Re-insert the removed points
            for (int i = 0; i < numPoints / 2; i++)
            {
                treeWithUniqueValues.Add(points[i], i);

                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                bool found = treeWithUniqueValues.TryFindNearest(points[i], out var iFound, out distance);
                Assert.IsTrue(found);
                Assert.AreEqual(iFound, i, "Could not find newly inserted point after deletes");
            }

            //Look for the remaining points
            for (int i = numPoints / 2; i < numPoints; i++)
            {
                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                bool found = treeWithUniqueValues.TryFindNearest(points[i], out var iFound, out distance);
                Assert.IsTrue(found);
                Assert.AreEqual(iFound, i, "Could not find previously inserted point after delete and insert");
            }

            //Delete all the points
            for (int i = 0; i < numPoints; i++)
            {
                bool Success = treeWithUniqueValues.TryRemove(i, out int Value);
                Debug.Assert(Success, "Could not remove previously inserted point");

                //Make sure if we look for the removed point we get an index higher than the ones we've already removed
                if (i < numPoints - 1)
                {
                    bool found = treeWithUniqueValues.TryFindNearest(points[i], out var iFound, out distance);
                    Assert.IsTrue(iFound > i, "Found previously deleted point"); 
                    Assert.IsFalse(treeWithUniqueValues.Contains(i));
                }
            }

            //Insert some points into the empty treeWithUniqueValues to make sure we still can 
            for (int i = 0; i < numPoints; i++)
            {
                points[i] = new GridVector2(RandGen.NextDouble() * BoundarySize, RandGen.NextDouble() * BoundarySize);
                treeWithUniqueValues.Add(points[i], i);
            }

            //Check to see we can find every item in the quad treeWithUniqueValues
            for (int i = 0; i < numPoints; i++)
            {
                Assert.IsTrue(treeWithUniqueValues.Contains(i));

                bool found = treeWithUniqueValues.TryFindNearest(points[i], out var iFound, out distance);
                Assert.IsTrue(found);
                Assert.AreEqual(iFound, i, "Could not find previously inserted point");
            }

            List<DistanceToPoint<int>> foundPoints = treeWithUniqueValues.FindNearestPoints(new GridVector2(BoundarySize * -2, BoundarySize * -2), treeWithUniqueValues.Count * 2);
            Assert.AreEqual(treeWithUniqueValues.Count, foundPoints.Count);

            foundPoints = treeWithUniqueValues.FindNearestPoints(GridVector2.Zero, treeWithUniqueValues.Count * 2);
            Assert.AreEqual(treeWithUniqueValues.Count, foundPoints.Count);

            //The end 
        }

        [TestMethod]
        public void QuadTreeFsCheck()
        {
            GeometryArbitraries.Register();

            Configuration config = Configuration.QuickThrowOnFailure;
            config.StartSize = 128;
            config.MaxNbOfTest = 250; 

            QuadTreeWithUniqueValuesSpec withUniqueValuesSpec = new QuadTreeWithUniqueValuesSpec();
            withUniqueValuesSpec.ToProperty().Check(config);

            /*
            Prop.ForAll<GridVector2[]>(points =>
            {
                
                QuadTreeWithUniqueValues<int> qTree = new QuadTreeWithUniqueValues<int>(points.BoundingBox());

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
