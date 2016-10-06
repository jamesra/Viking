using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphLibTest
{
    [TestClass]
    public class PathTest
    {
        public static SimpleGraph CreateGraphWithCycle()
        {
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
            SimpleGraph graph = new GraphLibTest.SimpleGraph();

            graph.AddNode(1);
            graph.AddNode(2);
            graph.AddNode(3);
            graph.AddNode(4);
            graph.AddNode(5);
            graph.AddNode(6);
            graph.AddNode(7);
            graph.AddNode(8);
            graph.AddNode(9);
            graph.AddNode(10);
            graph.AddNode(11);

            graph.AddEdge(1,2);
            graph.AddEdge(2,3);
            graph.AddEdge(3,4);
            graph.AddEdge(3,11);
            graph.AddEdge(11, 6);
            graph.AddEdge(4,5);
            graph.AddEdge(6,5);
            graph.AddEdge(6,7);
            graph.AddEdge(5,8);
            graph.AddEdge(9,10);

            return graph;
        }

        private bool IsPathEqual(IList<long> path, long[] expected_path)
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
            SimpleGraph graph = CreateGraphWithCycle();

            IList<long> path = SimpleGraph.Path(graph, 1, 8);
            long[] expected_path = new long[] { 1, 2, 3, 4, 5, 8 };

            Assert.IsTrue(IsPathEqual(path, expected_path));   
        }

        [TestMethod]
        public void TestPathAroundCycle3_5()
        {
            SimpleGraph graph = CreateGraphWithCycle();

            IList<long> path = SimpleGraph.Path(graph, 3, 5);
            long[] expected_path = new long[] { 3, 4, 5};

            Assert.IsTrue(IsPathEqual(path, expected_path));
        }

        [TestMethod]
        public void TestPathAroundCycle11_6()
        {
            SimpleGraph graph = CreateGraphWithCycle();

            IList<long> path = SimpleGraph.Path(graph, 11, 6);
            long[] expected_path = new long[] { 11, 6 };

            Assert.IsTrue(IsPathEqual(path, expected_path));
        }
        
        [TestMethod]
        public void TestPathAroundCycle8_8()
        {
            SimpleGraph graph = CreateGraphWithCycle();

            IList<long> path = SimpleGraph.Path(graph, 8, 8);
            long[] expected_path = new long[] {8 };

            Assert.IsTrue(IsPathEqual(path, expected_path));
        }

        [TestMethod]
        public void TestInvalidPath1_9()
        {
            SimpleGraph graph = CreateGraphWithCycle();

            IList<long> path = SimpleGraph.Path(graph, 1, 9);
            Assert.IsNull(path);
        }
    }
}
