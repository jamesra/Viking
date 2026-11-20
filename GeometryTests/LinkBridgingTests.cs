using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// <see cref="LinkBridging"/>: removed locations are replaced by direct links between the locations they joined, the
    /// mosaic remap's rule for locations on tiles a rebuilt mosaic dropped.
    /// </summary>
    [TestClass]
    public class LinkBridgingTests
    {
        /// <summary>A symmetric link graph from edges; every node in <paramref name="structures"/> belongs to the given structure.</summary>
        static (System.Func<long, IEnumerable<long>> Links, System.Func<long, long?> Structure) Graph(
            (long A, long B)[] edges, Dictionary<long, long> structures = null)
        {
            Dictionary<long, HashSet<long>> links = [];
            foreach ((long a, long b) in edges)
            {
                (links.TryGetValue(a, out var la) ? la : links[a] = []).Add(b);
                (links.TryGetValue(b, out var lb) ? lb : links[b] = []).Add(a);
            }

            return (id => links.TryGetValue(id, out var l) ? l : [],
                    id => structures is null ? 1 : structures.TryGetValue(id, out long s) ? s : null);
        }

        static string[] Pairs(IEnumerable<NodePair> pairs) => [.. pairs.Select(p => p.ToString()).OrderBy(s => s)];

        [TestMethod]
        public void MiddleOfChain_LinksItsTwoNeighbours()
        {
            var (links, structure) = Graph([(149, 150), (150, 151)]);
            var groups = LinkBridging.Bridge([150], links, structure);

            Assert.AreEqual(1, groups.Count);
            CollectionAssert.AreEqual(new long[] { 150 }, groups[0].Removed.ToArray());
            CollectionAssert.AreEqual(new long[] { 149, 151 }, groups[0].Neighbours.ToArray());
            CollectionAssert.AreEqual(new[] { "149-151" }, Pairs(groups[0].Added));
        }

        [TestMethod]
        public void RunOfRemovedLocations_IsOneGroupBridgedEndToEnd()
        {
            var (links, structure) = Graph([(1, 2), (2, 3), (3, 4), (4, 5)]);
            var groups = LinkBridging.Bridge([2, 3, 4], links, structure);

            Assert.AreEqual(1, groups.Count);
            CollectionAssert.AreEqual(new long[] { 2, 3, 4 }, groups[0].Removed.ToArray());
            CollectionAssert.AreEqual(new[] { "1-5" }, Pairs(groups[0].Added));
        }

        [TestMethod]
        public void BranchPoint_LinksEveryPairOfNeighbours()
        {
            var (links, structure) = Graph([(10, 1), (10, 2), (10, 3)]);
            var groups = LinkBridging.Bridge([10], links, structure);

            CollectionAssert.AreEqual(new[] { "1-2", "1-3", "2-3" }, Pairs(groups[0].Added));
        }

        [TestMethod]
        public void ChainEnd_AddsNothing()
        {
            var (links, structure) = Graph([(1, 2)]);
            var groups = LinkBridging.Bridge([2], links, structure);

            CollectionAssert.AreEqual(new long[] { 1 }, groups[0].Neighbours.ToArray());
            Assert.AreEqual(0, groups[0].Added.Count);
        }

        [TestMethod]
        public void UnlinkedLocation_IsAGroupOfItsOwn()
        {
            var (links, structure) = Graph([]);
            var groups = LinkBridging.Bridge([7], links, structure);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(0, groups[0].Neighbours.Count);
        }

        [TestMethod]
        public void SeparateChains_AreSeparateGroups()
        {
            var (links, structure) = Graph([(1, 2), (2, 3), (11, 12), (12, 13)]);
            var groups = LinkBridging.Bridge([12, 2], links, structure);

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEqual(new[] { "1-3" }, Pairs(groups[0].Added));
            CollectionAssert.AreEqual(new[] { "11-13" }, Pairs(groups[1].Added));
        }

        [TestMethod]
        public void NeighboursAlreadyLinked_AreNotAddedAgain()
        {
            var (links, structure) = Graph([(1, 2), (2, 3), (1, 3)]);
            var groups = LinkBridging.Bridge([2], links, structure);

            Assert.AreEqual(0, groups[0].Added.Count);
            CollectionAssert.AreEqual(new[] { "1-3" }, Pairs(groups[0].AlreadyLinked));
        }

        [TestMethod]
        public void NeighboursOfDifferentStructures_AreNotLinked()
        {
            var (links, structure) = Graph([(1, 2), (2, 3), (2, 4)], new Dictionary<long, long> { [1] = 100, [2] = 100, [3] = 100, [4] = 200 });
            var groups = LinkBridging.Bridge([2], links, structure);

            CollectionAssert.AreEqual(new[] { "1-3" }, Pairs(groups[0].Added));
            CollectionAssert.AreEqual(new[] { "1-4", "3-4" }, Pairs(groups[0].NotLinkedDifferentStructure));
        }
    }
}
