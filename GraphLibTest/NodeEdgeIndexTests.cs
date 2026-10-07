using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace GraphLibTest
{
    /// <summary>
    /// Pins how <see cref="GraphLib.Node{KEY, EDGETYPE}"/> indexes its edges by partner key as
    /// <see cref="GraphLib.Graph{KEY, NODETYPE, EDGETYPE}.AddEdge"/> and <c>RemoveEdge</c> run:
    /// a non-loop edge is filed under the other endpoint, a loop under the node's own key, and a
    /// partner key disappears when its last edge is removed.
    /// </summary>
    [TestClass]
    public class NodeEdgeIndexTests
    {
        private const int NodeCount = 5;

        /// <summary>
        /// One generated step: add the edge (Source, Target, Directional) when absent, or, when
        /// <see cref="Remove"/> is set and edges exist, remove the present edge chosen by <see cref="Pick"/>.
        /// <see cref="Reversed"/> removes an undirected edge through an equal instance with its endpoints swapped.
        /// </summary>
        public record EdgeOp(int Source, int Target, bool Directional, bool Remove, bool Reversed, int Pick);

        private static Arbitrary<EdgeOp[]> ArbOps()
        {
            Gen<EdgeOp> op =
                from s in Gen.Choose(0, NodeCount - 1)
                from t in Gen.Choose(0, NodeCount - 1)
                from d in Arb.Default.Bool().Generator
                from r in Gen.Frequency(
                    System.Tuple.Create(2, Gen.Constant(false)),
                    System.Tuple.Create(1, Gen.Constant(true)))
                from rev in Arb.Default.Bool().Generator
                from p in Gen.Choose(0, 1000)
                select new EdgeOp(s, t, d, r, rev, p);

            return Arb.From(Gen.ArrayOf(op));
        }

        private static SimpleGraph CreateGraph()
        {
            SimpleGraph graph = new();
            for (int i = 0; i < NodeCount; i++)
                graph.AddNode(i);
            return graph;
        }

        /// <summary>
        /// Checks every node's edge index against a reference built from the edges still present.
        /// </summary>
        private static bool IndexMatches(SimpleGraph graph, List<SimpleEdge> present)
        {
            for (long n = 0; n < NodeCount; n++)
            {
                Dictionary<long, List<SimpleEdge>> expected = present
                    .Where(e => e.SourceNodeKey == n || e.TargetNodeKey == n)
                    .GroupBy(e => e.SourceNodeKey == n ? e.TargetNodeKey : e.SourceNodeKey)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var actual = graph.Nodes[n].Edges;
                if (!new HashSet<long>(actual.Keys).SetEquals(expected.Keys))
                    return false;

                foreach (var pair in expected)
                {
                    if (!actual[pair.Key].SetEquals(pair.Value) || actual[pair.Key].Count != pair.Value.Count)
                        return false;
                }
            }

            return true;
        }

        [TestMethod]
        public void EdgeIndexMatchesPresentEdgesAfterAnyAddRemoveSequence()
        {
            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 500;

            Prop.ForAll(ArbOps(), ops =>
            {
                SimpleGraph graph = CreateGraph();
                List<SimpleEdge> present = [];

                foreach (EdgeOp op in ops)
                {
                    if (op.Remove && present.Count > 0)
                    {
                        SimpleEdge stored = present[op.Pick % present.Count];
                        SimpleEdge toRemove = op.Reversed && !stored.Directional
                            ? new SimpleEdge(stored.TargetNodeKey, stored.SourceNodeKey, false)
                            : stored;
                        graph.RemoveEdge(toRemove);
                        present.Remove(stored);
                    }
                    else
                    {
                        SimpleEdge edge = new(op.Source, op.Target, op.Directional);
                        if (graph.Edges.ContainsKey(edge))
                            continue;
                        graph.AddEdge(edge);
                        present.Add(edge);
                    }

                    if (!IndexMatches(graph, present))
                        return false;
                }

                return true;
            }).Check(config);
        }

        [TestMethod]
        public void LoopIsIndexedUnderOwnKeyAndRemovedWithLastEdge()
        {
            SimpleGraph graph = CreateGraph();
            SimpleEdge loop = new(3, 3, false);
            SimpleEdge directedLoop = new(3, 3, true);

            graph.AddEdge(loop);
            graph.AddEdge(directedLoop);

            CollectionAssert.AreEqual(new long[] { 3 }, graph.Nodes[3].Edges.Keys.ToArray());
            Assert.AreEqual(2, graph.Nodes[3].Edges[3].Count);

            graph.RemoveEdge(loop);
            Assert.AreEqual(1, graph.Nodes[3].Edges[3].Count);

            graph.RemoveEdge(directedLoop);
            Assert.AreEqual(0, graph.Nodes[3].Edges.Count);
        }

        [TestMethod]
        public void NonLoopEdgeIsIndexedUnderTheOtherEndpointOnBothNodes()
        {
            SimpleGraph graph = CreateGraph();
            graph.AddEdge(new SimpleEdge(1, 4, true));

            CollectionAssert.AreEqual(new long[] { 4 }, graph.Nodes[1].Edges.Keys.ToArray());
            CollectionAssert.AreEqual(new long[] { 1 }, graph.Nodes[4].Edges.Keys.ToArray());

            graph.RemoveEdge(new SimpleEdge(1, 4, true));
            Assert.AreEqual(0, graph.Nodes[1].Edges.Count);
            Assert.AreEqual(0, graph.Nodes[4].Edges.Count);
        }
    }
}
