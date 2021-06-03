using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace GraphLibTest
{
    [TestClass]
    public class SubgraphTest
    {
        public static SimpleGraph CreateGraphWithIsolations()
        {
            SimpleGraph graph = new GraphLibTest.SimpleGraph();
            
            graph.AddNode(1);
            graph.AddNode(2);
            graph.AddNode(3);

            graph.AddNode(11);
            graph.AddNode(12);
            graph.AddNode(13);

            graph.AddEdge(1,2);
            graph.AddEdge(2,3);

            graph.AddEdge(11,12);
            graph.AddEdge(12,13);

            return graph;
        }

        [TestMethod]
        public void TestIsolatedSubgraphButEmpty()
        {
            SimpleGraph graph = new GraphLibTest.SimpleGraph();
            IList<SortedSet<long>> subgraphs = SimpleGraph.IsolatedSubgraphs(graph);

            Assert.AreEqual(subgraphs.Count, 0);
        }

        [TestMethod]
        public void TestIsolatedSubgraphButOneNode()
        {
            SimpleGraph graph = new GraphLibTest.SimpleGraph();
            SimpleNode A1 = new SimpleNode(1);
            graph.AddNode(A1);

            IList<SortedSet<long>> subgraphs = SimpleGraph.IsolatedSubgraphs(graph);

            Assert.AreEqual(subgraphs.Count, 1);
            Assert.AreEqual(subgraphs[0].Count, 1);
        }


        [TestMethod]
        public void TestIsolatedSubgraph()
        {
            SimpleGraph graph = CreateGraphWithIsolations();

            IList<SortedSet<long>> subgraphs = SimpleGraph.IsolatedSubgraphs(graph);

            Assert.AreEqual(subgraphs.Count, 2);
            Assert.AreEqual(subgraphs[0].Count, 3);
            Assert.AreEqual(subgraphs[1].Count, 3);

            long TotalNodes = subgraphs.Select(L => L.Count).Sum();
            Assert.AreEqual(TotalNodes, graph.Nodes.Count);

            //Make sure the lists are mutually exclusive
            foreach (long Key in subgraphs[0])
            {
                Assert.IsFalse(subgraphs[1].Contains(Key));
            }

            //Make sure the lists are mutually exclusive
            foreach (long Key in subgraphs[1])
            {
                Assert.IsFalse(subgraphs[0].Contains(Key));
            }
        }
    }
}
