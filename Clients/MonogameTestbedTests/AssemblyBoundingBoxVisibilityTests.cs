using Microsoft.VisualStudio.TestTools.UnitTesting;
using MonogameTestbed;
using MorphologyMesh;

namespace MonogameTestbedTests
{
    /// <summary>
    /// Guards the visibility rule behind the slice-assembly bounding box overlay in BajajMultiTest.
    /// </summary>
    /// <remarks>
    /// The overlay silently showed nothing for a long time because the rule returned the XOR of the two sibling
    /// completion flags. That never consulted the node it was asked about, and before any slice finished both
    /// flags were true, so the XOR was false for every non-root node. These tests pin the startup case in
    /// particular, since that is the state in which the regression was invisible.
    /// </remarks>
    [TestClass]
    public class AssemblyBoundingBoxVisibilityTests
    {
        /// <summary>
        /// Assigning MeshModel is what flips MeshComplete, and the planner deliberately treats a null mesh as
        /// "finished but produced nothing", so this marks a node complete the same way the real pipeline does.
        /// </summary>
        private static void MarkComplete(IAssemblyPlannerNode node) => node.MeshModel = new SliceGraphMeshModel();

        /// <summary>
        /// Builds a two-level tree: root over two branches, each over two leaves.
        /// </summary>
        private static AssemblyPlannerBranch BuildTree(out AssemblyPlannerLeaf[] leaves)
        {
            leaves =
            [
                new AssemblyPlannerLeaf(1),
                new AssemblyPlannerLeaf(2),
                new AssemblyPlannerLeaf(3),
                new AssemblyPlannerLeaf(4)
            ];

            AssemblyPlannerBranch left = new(leaves[0], leaves[1]);
            AssemblyPlannerBranch right = new(leaves[2], leaves[3]);
            leaves[0].Parent = left;
            leaves[1].Parent = left;
            leaves[2].Parent = right;
            leaves[3].Parent = right;

            AssemblyPlannerBranch root = new(left, right);
            left.Parent = root;
            right.Parent = root;
            return root;
        }

        [TestMethod]
        public void EveryNodeIsBoxedBeforeAnySliceCompletes()
        {
            var root = BuildTree(out var leaves);

            foreach (var leaf in leaves)
                Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaf),
                    $"Leaf {leaf.Key} should be boxed before any slice is meshed.");

            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root.Left));
            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root.Right));
            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root));
        }

        [TestMethod]
        public void CompletingOneLeafHidesOnlyThatLeaf()
        {
            var root = BuildTree(out var leaves);

            MarkComplete(leaves[0]);

            Assert.IsFalse(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaves[0]),
                "A meshed slice should lose its box.");
            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaves[1]),
                "A sibling's completion must not hide an unmeshed slice.");
            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaves[2]));
            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaves[3]));
            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root));
        }

        [TestMethod]
        public void SiblingsAreJudgedIndependently()
        {
            BuildTree(out var leaves);

            MarkComplete(leaves[0]);

            //The XOR rule gave both siblings the same answer, so this pairing is the direct regression check.
            Assert.AreNotEqual(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaves[0]),
                MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaves[1]),
                "One sibling is meshed and the other is not, so their visibility must differ.");
        }

        [TestMethod]
        public void BothSiblingsCompleteHidesBothButKeepsUnmergedParent()
        {
            var root = BuildTree(out var leaves);

            MarkComplete(leaves[0]);
            MarkComplete(leaves[1]);

            Assert.IsFalse(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaves[0]));
            Assert.IsFalse(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(leaves[1]));
            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root.Left),
                "The branch still owes a merged mesh, so it stays boxed.");
            Assert.IsTrue(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root));
        }

        [TestMethod]
        public void FullyAssembledTreeShowsNoBoxes()
        {
            var root = BuildTree(out var leaves);

            foreach (var leaf in leaves)
                MarkComplete(leaf);
            MarkComplete(root.Left);
            MarkComplete(root.Right);
            MarkComplete(root);

            Assert.IsFalse(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root),
                "A finished assembly should leave the solid mesh unobscured.");
            Assert.IsFalse(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root.Left));
            Assert.IsFalse(MeshAssemblyPlannerIncompleteView.CanShowBoundingBoxModel(root.Right));
        }
    }
}
