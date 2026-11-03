using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="RegistrationTree.AddPair"/>, which orders section-to-section registration in
    /// <c>Volume</c>: control sections with no known parent are roots, and every mapped section records its control section as parent.
    /// </summary>
    [TestClass]
    public class RegistrationTreeTests
    {
        [TestMethod]
        public void AddPair_ChainHasOneRootAndEachNodeKnowsItsParent()
        {
            var tree = new RegistrationTree();
            tree.AddPair(1, 2);
            tree.AddPair(2, 3);

            CollectionAssert.AreEqual(new[] { 1 }, tree.RootNodes.Keys.ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, tree.Nodes.Keys.ToArray());
            CollectionAssert.AreEqual(new[] { 2 }, tree.Nodes[1].Children);
            CollectionAssert.AreEqual(new[] { 3 }, tree.Nodes[2].Children);
            Assert.AreEqual(1, tree.Nodes[2].Parent);
            Assert.AreEqual(2, tree.Nodes[3].Parent);
            Assert.IsNull(tree.Nodes[1].Parent);
        }

        [TestMethod]
        public void AddPair_MappedSectionSeenFirstIsDemotedFromRoot()
        {
            var tree = new RegistrationTree();
            tree.AddPair(5, 6);
            tree.AddPair(4, 5);

            CollectionAssert.AreEqual(new[] { 4 }, tree.RootNodes.Keys.ToArray());
            Assert.AreEqual(4, tree.Nodes[5].Parent);
            Assert.AreEqual(5, tree.Nodes[6].Parent);
        }

        [TestMethod]
        public void AddPair_SiblingsShareControlSection()
        {
            var tree = new RegistrationTree();
            tree.AddPair(10, 11);
            tree.AddPair(10, 12);

            CollectionAssert.AreEqual(new[] { 10 }, tree.RootNodes.Keys.ToArray());
            CollectionAssert.AreEqual(new[] { 11, 12 }, tree.Nodes[10].Children);
        }
    }
}
