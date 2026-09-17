using GraphLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

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
