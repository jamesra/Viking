using GraphLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraphLibTest
{


    /*
     * //CreateGraphWithCycle()
     * 
     *  1 - 2  - 3 - 11
     *          /    |
     *         /     |
     *        /      |
     *       4       6 - 7
     *        \     /
     *         \   /
     *          \ /
     *     9     5
     *     |     | 
     *     10    6
     */

    [TestClass]
    public class PathTest
    {


        private static bool IsPathEqual(IList<long> path, long[] expected_path)
        {
            if (path.Count != expected_path.Length)
                return false;

            for (int i = 0; i < expected_path.Length; i++)
            {
                if (path[i] != expected_path[i])
                    return false;
            }

            return true;
        }

        [TestMethod]
        public void ShortestPathStaticOverloadAgreesWithExtension()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();

            CollectionAssert.AreEqual(
                graph.ShortestPath(1, 8).ToArray(),
                SimpleGraph.ShortestPath(graph, 1, 8).ToArray());

            Func<SimpleNode, bool> isMatch = n => n.Key == 8;
            CollectionAssert.AreEqual(
                graph.ShortestPath(1, isMatch).ToArray(),
                SimpleGraph.ShortestPath(graph, 1, isMatch).ToArray());
        }

        [TestMethod]
        public void TestPathAroundCycle1_8()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();

            IList<long> path = SimpleGraph.ShortestPath(graph, 1, 8);
            long[] expected_path = [1, 2, 3, 4, 5, 8];

            Assert.IsTrue(IsPathEqual(path, expected_path));
        }

        [TestMethod]
        public void TestPathAroundCycle3_5()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();

            IList<long> path = SimpleGraph.ShortestPath(graph, 3, 5);
            long[] expected_path = [3, 4, 5];

            Assert.IsTrue(IsPathEqual(path, expected_path));
        }

        [TestMethod]
        public void TestPathAroundCycle11_6()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();

            IList<long> path = SimpleGraph.ShortestPath(graph, 11, 6);
            long[] expected_path = [11, 6];

            Assert.IsTrue(IsPathEqual(path, expected_path));
        }

        [TestMethod]
        public void TestPathAroundCycle8_8()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();

            IList<long> path = SimpleGraph.ShortestPath(graph, 8, 8);
            long[] expected_path = [8];

            Assert.IsTrue(IsPathEqual(path, expected_path));
        }

        [TestMethod]
        public void TestInvalidPath1_9()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();

            IList<long> path = SimpleGraph.ShortestPath(graph, 1, 9);
            Assert.IsNull(path);
        }

        /// <summary>
        /// Builds a graph with two routes of equal hop count from 1 to 4 (via 2 and via 3) by adding edges in
        /// the given order, so tests can show that the tie-break does not depend on insertion order.
        /// </summary>
        private static SimpleGraph CreateDiamond(params (long Source, long Target)[] edges)
        {
            SimpleGraph graph = new();
            foreach (long id in new long[] { 1, 2, 3, 4 })
                graph.AddNode(id);

            foreach ((long source, long target) in edges)
                graph.AddEdge(source, target);

            return graph;
        }

        /// <summary>
        /// Pins the tie-break of <see cref="SimpleGraph.ShortestPath(SimpleGraph, long, long)"/>: among equal-length
        /// routes the one through the lowest neighbor key wins, because RecursePath enumerates a sorted key set.
        /// Callers that display or cache the path rely on it being the same on every run.
        /// </summary>
        [TestMethod]
        public void TestEqualLengthPathsPickLowestNeighborKey()
        {
            SimpleGraph graph = CreateDiamond((1, 2), (2, 4), (1, 3), (3, 4));

            IList<long> path = SimpleGraph.ShortestPath(graph, 1, 4);

            Assert.IsTrue(IsPathEqual(path, [1, 2, 4]));
        }

        [TestMethod]
        public void TestEqualLengthPathsIgnoreEdgeInsertionOrder()
        {
            SimpleGraph graph = CreateDiamond((1, 3), (3, 4), (1, 2), (2, 4));

            IList<long> path = SimpleGraph.ShortestPath(graph, 1, 4);

            Assert.IsTrue(IsPathEqual(path, [1, 2, 4]));
        }

        [TestMethod]
        public void TestEqualLengthPathsBreakTieFromOtherEnd()
        {
            SimpleGraph graph = CreateDiamond((1, 2), (2, 4), (1, 3), (3, 4));

            IList<long> path = SimpleGraph.ShortestPath(graph, 4, 1);

            Assert.IsTrue(IsPathEqual(path, [4, 2, 1]));
        }

        [TestMethod]
        public void TestCycleDetection1()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();
            IList<long> path = graph.FindCycle(3);
            long[] expected_path = [11, 6, 5, 4, 3, 11];
            long[] reverse_expected_path = [4, 5, 6, 11, 3, 4];
            Assert.IsTrue(IsPathEqual(path, expected_path) || IsPathEqual(path, reverse_expected_path));
        }

        [TestMethod]
        public void TestCycleDetection2()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();
            IList<long> path = graph.FindCycle(2);
            Assert.IsNull(path);
        }

        [TestMethod]
        public void TestCycleDetection3()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();
            IList<long> path = graph.FindCycle(7);
            Assert.IsNull(path);
        }

        [TestMethod]
        public void TestAddRemoveEdges()
        {
            /*CreateGraphWithCycle() */
            /*
             *  1 - 2  - 3 - 11
             *          /    |
             *         /     |
             *        /      |
             *       4       6 - 7
             *        \     /
             *         \   /
             *          \ /
             *     9     5
             *     |     | 
             *     10    6
             */

            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();

            IList<long> path = SimpleGraph.ShortestPath(graph, 1, 7);
            long[] expected_short_path = [1, 2, 3, 11, 6, 7];
            Assert.IsTrue(IsPathEqual(path, expected_short_path));

            //Remove 11-6
            SimpleEdge edgeToRemove = new(6, 11);
            graph.RemoveEdge(edgeToRemove);

            Assert.IsFalse(graph.Nodes[11].Edges.ContainsKey(6));
            Assert.IsFalse(graph.Nodes[6].Edges.ContainsKey(11));

            IList<long> new_path = SimpleGraph.ShortestPath(graph, 1, 7);
            long[] expected_long_path = [1, 2, 3, 4, 5, 6, 7];
            Assert.IsTrue(IsPathEqual(new_path, expected_long_path));

            //----------- Add a directional path ---

            SimpleEdge directionalEdge = new(6, 11, true);
            graph.AddEdge(directionalEdge);

            Assert.IsTrue(graph.Nodes[11].Edges.ContainsKey(6));
            Assert.IsTrue(graph.Nodes[6].Edges.ContainsKey(11));

            //Long way against the direction
            IList<long> long_path = SimpleGraph.ShortestPath(graph, 1, 7);
            Assert.IsTrue(IsPathEqual(long_path, expected_long_path));

            //Shortcut using the direction
            IList<long> short_path = SimpleGraph.ShortestPath(graph, 7, 1);
            long[] expected_short_reversed_path = [7, 6, 11, 3, 2, 1];
            Assert.IsTrue(IsPathEqual(short_path, expected_short_reversed_path));

            //----------- Add a second directional edge, making the link effectively bidirectional ------
            SimpleEdge directionalEdge2 = new(11, 6, true);
            graph.AddEdge(directionalEdge2);

            IList<long> restored_short_path = SimpleGraph.ShortestPath(graph, 1, 7);
            Assert.IsTrue(IsPathEqual(restored_short_path, expected_short_path));
        }

        [TestMethod]
        public void TestAddRemoveNodes()
        {
            /*CreateGraphWithCycle() */
            /*
             *  1 - 2  - 3 - 11
             *          /    |
             *         /     |
             *        /      |
             *       4       6 - 7
             *        \     /
             *         \   /
             *          \ /
             *     9     5
             *     |     | 
             *     10    6
             */

            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();

            long NodeToRemove = 11;
            IList<long> path = SimpleGraph.ShortestPath(graph, 1, 7);
            long[] expected_short_path = [1, 2, 3, 11, 6, 7];
            Assert.IsTrue(IsPathEqual(path, expected_short_path));

            ICollection<long> partners = [.. graph.Nodes[NodeToRemove].Edges.Keys];
            Assert.AreEqual(2, partners.Count);

            graph.RemoveNode(NodeToRemove);
            VerifyNodeRemoved(graph, NodeToRemove, partners);

            IList<long> new_path = SimpleGraph.ShortestPath(graph, 1, 7);
            long[] expected_long_path = [1, 2, 3, 4, 5, 6, 7];
            Assert.IsTrue(IsPathEqual(new_path, expected_long_path));

            graph.AddNode(11);
            graph.AddEdge(3, 11);
            graph.AddEdge(11, 6);
            IList<long> restored_path = SimpleGraph.ShortestPath(graph, 1, 7);
            Assert.IsTrue(IsPathEqual(restored_path, expected_short_path));
        }

        private static void VerifyNodeRemoved(SimpleGraph graph, long removed_id, ICollection<long> partners)
        {
            Assert.IsFalse(graph.Nodes.ContainsKey(removed_id));
            foreach (long partner in partners)
            {
                Assert.IsFalse(graph.Nodes[partner].Edges.ContainsKey(removed_id));
            }
        }
    }
}
