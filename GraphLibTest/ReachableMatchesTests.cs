using FsCheck;
using GraphLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace GraphLibTest
{
    /// <summary>
    /// Pins <see cref="GraphPathExtensions.ConnectedNodes{KEY, NODETYPE, EDGETYPE}"/> (undirected
    /// multi-hop reachability) and <see cref="GraphPathExtensions.FindReachableMatches{KEY, NODETYPE, EDGETYPE}"/>
    /// (matches reachable without walking through another match), which the morphology boundary finder
    /// calls on its medial axis graph. Two tests pin directional-edge travel in
    /// <see cref="Graph{KEY, NODETYPE, EDGETYPE}.RecurseReachableNodes"/>: each neighbor is checked on
    /// <c>Edges[linked_node]</c> (aligned with <see cref="Graph{KEY, NODETYPE, EDGETYPE}.RecursePath"/>),
    /// continuing to other neighbors when one edge is blocked and not walking a blocked edge because
    /// another neighbor is open.
    /// </summary>
    [TestClass]
    public class ReachableMatchesTests
    {
        private const int RandomNodeCount = 7;

        /// <summary>An undirected edge between two generated node ids.</summary>
        public record EdgePair(int A, int B);

        private static SimpleGraph CreateChain(int nodeCount)
        {
            SimpleGraph graph = new();
            for (long i = 0; i < nodeCount; i++)
                graph.AddNode(i);
            for (long i = 0; i + 1 < nodeCount; i++)
                graph.AddEdge(i, i + 1);
            return graph;
        }

        private static SimpleGraph CreateNodes(params long[] ids)
        {
            SimpleGraph graph = new();
            foreach (long id in ids)
                graph.AddNode(id);
            return graph;
        }

        private static long[] Matches(SimpleGraph graph, long origin, params long[] matchIds)
        {
            HashSet<long> match = [.. matchIds];
            return [.. graph.FindReachableMatches(origin, n => match.Contains(n.Key))];
        }

        private static long[] Connected(SimpleGraph graph, long root, IEnumerable<long> seed = null)
        {
            SortedSet<long> set = seed == null ? [] : [.. seed];
            graph.ConnectedNodes(ref set, graph.Nodes[root]);
            return [.. set];
        }

        [TestMethod]
        public void ConnectedNodesReachesEveryNodeOfTheComponentFromAnyMember()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();
            long[] component = [1, 2, 3, 4, 5, 6, 7, 8, 11];

            foreach (long root in component)
                CollectionAssert.AreEqual(component, Connected(graph, root), $"root {root}");

            CollectionAssert.AreEqual(new long[] { 9, 10 }, Connected(graph, 9));
        }

        [TestMethod]
        public void ConnectedNodesOfIsolatedNodeIsItself()
        {
            SimpleGraph graph = CreateNodes(1, 2);

            CollectionAssert.AreEqual(new long[] { 1 }, Connected(graph, 1));
        }

        [TestMethod]
        public void ConnectedNodesFollowsDirectionalEdgesBackwards()
        {
            SimpleGraph graph = CreateNodes(1, 2, 3);
            graph.AddEdge(new SimpleEdge(1, 2, true));
            graph.AddEdge(new SimpleEdge(3, 2, true));

            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, Connected(graph, 2));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, Connected(graph, 1));
        }

        [TestMethod]
        public void ConnectedNodesReturnsUnchangedWhenRootAlreadyInSet()
        {
            SimpleGraph graph = CreateChain(4);

            CollectionAssert.AreEqual(new long[] { 0 }, Connected(graph, 0, [0]));
        }

        [TestMethod]
        public void ConnectedNodesDoesNotWalkThroughNodesAlreadyInSet()
        {
            SimpleGraph graph = CreateChain(5);

            CollectionAssert.AreEqual(new long[] { 0, 1, 3 }, Connected(graph, 0, [1, 3]));
            CollectionAssert.AreEqual(new long[] { 0, 1 }, Connected(graph, 0, [1]));
        }

        [TestMethod]
        public void ConnectedNodesKeepsNodesAlreadyInSetThatAreNotConnected()
        {
            SimpleGraph graph = CreateNodes(1, 2, 3, 9);
            graph.AddEdge(1, 2);

            CollectionAssert.AreEqual(new long[] { 1, 2, 9 }, Connected(graph, 1, [9]));
        }

        [TestMethod]
        public void ConnectedNodesMatchesBreadthFirstReferenceOnRandomGraphs()
        {
            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 300;

            Gen<EdgePair> pair =
                from a in Gen.Choose(0, RandomNodeCount - 1)
                from b in Gen.Choose(0, RandomNodeCount - 1)
                select new EdgePair(a, b);

            Prop.ForAll(Arb.From(Gen.ArrayOf(pair)), Arb.From(Gen.Choose(0, RandomNodeCount - 1)), (EdgePair[] pairs, int root) =>
            {
                SimpleGraph graph = new();
                for (int i = 0; i < RandomNodeCount; i++)
                    graph.AddNode(i);
                foreach (EdgePair p in pairs)
                {
                    SimpleEdge edge = new(p.A, p.B);
                    if (!graph.Edges.ContainsKey(edge))
                        graph.AddEdge(edge);
                }

                HashSet<long> expected = [root];
                Queue<long> queue = new([(long)root]);
                while (queue.Count > 0)
                {
                    long n = queue.Dequeue();
                    foreach (EdgePair p in pairs)
                    {
                        long other = p.A == n ? p.B : p.B == n ? p.A : -1;
                        if (other >= 0 && expected.Add(other))
                            queue.Enqueue(other);
                    }
                }

                return Connected(graph, root).SequenceEqual(expected.OrderBy(k => k));
            }).Check(config);
        }

        [TestMethod]
        public void FindReachableMatchesReturnsOriginWhenOriginMatches()
        {
            SimpleGraph graph = CreateChain(4);

            CollectionAssert.AreEqual(new long[] { 0 }, Matches(graph, 0, 0, 1, 2));
        }

        [TestMethod]
        public void FindReachableMatchesFindsMatchOnNeighborAndStopsThere()
        {
            SimpleGraph graph = CreateChain(5);

            CollectionAssert.AreEqual(new long[] { 1 }, Matches(graph, 0, 1));
            CollectionAssert.AreEqual(new long[] { 1 }, Matches(graph, 0, 1, 2, 3, 4));
        }

        [TestMethod]
        public void FindReachableMatchesCollectsOneMatchPerBranchWithoutPassingOverMatches()
        {
            // 1 -- 2 -- 4
            // |    |
            // 3    5 -- 6
            SimpleGraph graph = CreateNodes(1, 2, 3, 4, 5, 6);
            graph.AddEdge(1, 2);
            graph.AddEdge(1, 3);
            graph.AddEdge(2, 4);
            graph.AddEdge(2, 5);
            graph.AddEdge(5, 6);

            CollectionAssert.AreEqual(new long[] { 3, 4, 6 }, Matches(graph, 1, 3, 4, 6));
            CollectionAssert.AreEqual(new long[] { 2, 3 }, Matches(graph, 1, 2, 3, 4, 5, 6));
        }

        [TestMethod]
        public void FindReachableMatchesVisitsEachNodeOnceAroundACycle()
        {
            // 1 -- 2 -- 4
            // |         |
            // 3 -------/
            SimpleGraph graph = CreateNodes(1, 2, 3, 4);
            graph.AddEdge(1, 2);
            graph.AddEdge(1, 3);
            graph.AddEdge(2, 4);
            graph.AddEdge(3, 4);

            CollectionAssert.AreEqual(new long[] { 4 }, Matches(graph, 1, 4));
        }

        [TestMethod]
        public void FindReachableMatchesStaticOverloadAgreesWithExtension()
        {
            SimpleGraph graph = SimpleGraph.CreateGraphWithCycle();
            System.Func<SimpleNode, bool> isMatch = n => n.Key == 5 || n.Key == 7;

            CollectionAssert.AreEqual(
                graph.FindReachableMatches(1, isMatch).ToArray(),
                SimpleGraph.FindReachableMatches(graph, 1, isMatch).ToArray());
            CollectionAssert.AreEqual(new long[] { 5, 7 }, SimpleGraph.FindReachableMatches(graph, 1, isMatch).ToArray());
        }

        [TestMethod]
        public void FindReachableMatchesIsEmptyWhenNoMatchIsReachable()
        {
            SimpleGraph graph = CreateChain(3);
            graph.AddNode(9);

            Assert.AreEqual(0, Matches(graph, 0, 9).Length);
            Assert.AreEqual(0, Matches(graph, 0).Length);
        }

        [TestMethod]
        public void FindReachableMatchesTravelsDirectionalEdgeOnlyFromItsSource()
        {
            SimpleGraph graph = CreateNodes(1, 2, 3);
            graph.AddEdge(new SimpleEdge(1, 2, true));
            graph.AddEdge(2, 3);

            CollectionAssert.AreEqual(new long[] { 3 }, Matches(graph, 1, 3));
            Assert.AreEqual(0, Matches(graph, 3, 1).Length);
        }

        [TestMethod]
        public void FindReachableMatchesVisitsOtherNeighborsWhenEdgeToLowestNeighborIsBlocked()
        {
            // A blocked edge to the lowest unvisited neighbor must not prevent visiting other neighbors.
            SimpleGraph graph = CreateNodes(1, 2, 3);
            graph.AddEdge(new SimpleEdge(2, 1, true));
            graph.AddEdge(1, 3);

            CollectionAssert.AreEqual(new long[] { 3 }, Matches(graph, 1, 3));
        }

        [TestMethod]
        public void FindReachableMatchesDoesNotTravelBlockedDirectionalEdgeWhenOtherNeighborsAreOpen()
        {
            // Each neighbor is checked on its own edge; 3 -> 1 cannot be travelled from 1 even when 1 -> 2 is open.
            SimpleGraph graph = CreateNodes(1, 2, 3);
            graph.AddEdge(1, 2);
            graph.AddEdge(new SimpleEdge(3, 1, true));

            Assert.AreEqual(0, Matches(graph, 1, 3).Length);
        }

        [TestMethod]
        public void FindReachableMatchesOnChainFindsOnlyTheFirstMatchFromTheOrigin()
        {
            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 300;

            Prop.ForAll(Arb.From(Gen.Choose(1, 12)), Arb.From(Gen.ArrayOf(Arb.Default.Bool().Generator)), (int length, bool[] flags) =>
            {
                SimpleGraph graph = CreateChain(length);
                HashSet<long> match = [.. Enumerable.Range(0, length).Where(i => i < flags.Length && flags[i]).Select(i => (long)i)];

                long[] expected = match.Count == 0 ? [] : [match.Min()];
                return Matches(graph, 0, [.. match]).SequenceEqual(expected);
            }).Check(config);
        }
    }
}
