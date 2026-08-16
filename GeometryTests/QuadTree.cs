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
            Rectangle border = new(-10, 10, -10, 10);
            QuadTreeWithUniqueValues<int> tree = new(border);
            Assert.AreEqual(0, tree.Count);

            Vector2 p = new(0, 0);
            int value = 0;
            tree.Add(p, value);
            Assert.AreEqual(1, tree.Count);
            Assert.IsTrue(tree.Contains(p));
            Assert.IsTrue(tree.Contains(0));
            Assert.AreEqual(value, tree[p]);


            bool removed = tree.TryRemove(0, out var found);
            Assert.IsTrue(removed);
            Assert.AreEqual(0, tree.Count);
            Assert.AreEqual(value, found);
            Assert.IsFalse(tree.Contains(p));
            Assert.IsFalse(tree.Contains(0));
        }

        [TestMethod]
        public void QuadTreeTestSimpleAddRemoveUpdate()
        {
            Rectangle border = new(-10, 10, -10, 10);
            QuadTreeWithUniqueValues<int> tree = new(border);
            Assert.AreEqual(0, tree.Count);
            Vector2 p = new(0, 0);
            int value = 0;
            tree.Add(p, value);
            Assert.AreEqual(1, tree.Count);
            Assert.IsTrue(tree.Contains(p));
            Assert.IsTrue(tree.Contains(0));
            Assert.AreEqual(value, tree[p]);

            int updated_value = 1;
            tree[p] = updated_value;
            Assert.AreEqual(updated_value, tree[p]);

            int missing_value = 2;
            Vector2 missing_point = new(1, 1);
            try
            {
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
            Vector2 second_p = new(-1, 0);

            tree.Add(second_p, second_value);

            bool removed = tree.TryRemove(updated_value, out var found);
            Assert.IsTrue(removed);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual(updated_value, found);
            Assert.IsFalse(tree.Contains(p));
            Assert.IsFalse(tree.Contains(updated_value));

            bool point_removed = tree.TryRemove(second_p, out var removed_value);
            Assert.IsTrue(point_removed);
            Assert.AreEqual(removed_value, second_value);

            point_removed = tree.TryRemove(missing_point, out var removed_missing_value);
            Assert.IsFalse(point_removed);
        }

        [TestMethod]
        public void QuadTreeTestOne()
        {
            Vector2[] points = [ new(0,0),
                                                       new(1,1),
                                                       new(-10,-10),
                                                       new(-7.5, 2.5),
                                                       new(8.5, -1.5),
                                                       new(3.5, -6.5),
                                                       new(1.5, -8.5),
                                                       new(10, 10)];
            int[] values = [0, 1, 2, 3, 4, 5, 6, 7];
            Rectangle border = Vector2.Border(points);
            QuadTreeWithUniqueValues<int> treeWithUniqueValues = new(points, values, border);

            //Start with a basic test ensuring we can find all the existing points
            for (int i = 0; i < points.Length; i++)
            {
                bool found = treeWithUniqueValues.TryFindNearest(points[i], out var retValue, out double distance);

                Assert.IsTrue(found);
                Assert.AreEqual(i, retValue);
                Assert.AreEqual(0, distance);
            }

            //Check to see if we can find nearby points
            Vector2[] nearpoints = [ new(.25,.25),
                                                       new(.5,.51),
                                                       new(-7.5,-7.5),
                                                       new(-7.5, -1.5),
                                                       new(8.5, -5.5),
                                                       new(4.5, -7.75),
                                                       new(1, -8.75),
                                                       new(11, 11)]; //Out of original boundaries


            for (int i = 0; i < nearpoints.Length; i++)
            {
                bool found = treeWithUniqueValues.TryFindNearest(nearpoints[i], out var retValue, out var distance);

                Assert.IsTrue(found);
                Assert.AreEqual(i, retValue);
                Assert.AreEqual(Vector2.Distance(points[i], nearpoints[i]), distance);
            }

            //Check to see if we can return all points in a rectangle
            Rectangle gridRect = new(0, 15, 0, 15);
            treeWithUniqueValues.Intersect(gridRect, out List<Vector2> intersectPoints, out List<int> intersectValues);
            Assert.IsTrue(intersectValues.Contains(0));
            Assert.IsTrue(intersectValues.Contains(1));
            Assert.IsTrue(intersectValues.Contains(7));

            Assert.IsFalse(intersectValues.Contains(2));
            Assert.IsFalse(intersectValues.Contains(3));
            Assert.IsFalse(intersectValues.Contains(4));
            Assert.IsFalse(intersectValues.Contains(5));
            Assert.IsFalse(intersectValues.Contains(6));

        }

        [TestMethod]
        public void QuadTreeTestTwo()
        {
            int numPoints = 1000;
            double BoundarySize = 1000;
            int seed = 0;
            System.Random RandGen = new(seed);

            QuadTreeWithUniqueValues<int> treeWithUniqueValues = new(new Rectangle(-BoundarySize, BoundarySize, -BoundarySize, BoundarySize));

            Vector2[] points = new Vector2[numPoints];

            //Create the QuadTreeWithUniqueValues
            for (int i = 0; i < numPoints; i++)
            {
                points[i] = new Vector2(RandGen.NextDouble() * BoundarySize, RandGen.NextDouble() * BoundarySize);
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
                Assert.IsTrue(Success, "Could not remove previously inserted point");

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
                points[i] = new Vector2(RandGen.NextDouble() * BoundarySize, RandGen.NextDouble() * BoundarySize);
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

            List<DistanceToPoint<int>> foundPoints = treeWithUniqueValues.FindNearestPoints(new Vector2(BoundarySize * -2, BoundarySize * -2), treeWithUniqueValues.Count * 2);
            Assert.AreEqual(treeWithUniqueValues.Count, foundPoints.Count);

            foundPoints = treeWithUniqueValues.FindNearestPoints(Vector2.Zero, treeWithUniqueValues.Count * 2);
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

            QuadTreeWithUniqueValuesSpec withUniqueValuesSpec = new();
            withUniqueValuesSpec.ToProperty().Check(config);

            /*
            Prop.ForAll<Vector2[]>(points =>
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
