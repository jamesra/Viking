using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using WebAnnotation.UI;

namespace WebAnnotationTests
{
    /// <summary>
    /// Tests for the structure types hidden with the eye in the Structure Types panel.
    /// </summary>
    [TestClass]
    public class HiddenStructureTypeSetTests
    {
        // 1 -> 2, 3; 2 -> 4; 5 is a separate root.
        private static readonly Dictionary<long, long[]> Children = new() { [1] = [2, 3], [2] = [4] };

        private static IEnumerable<long> ChildrenOf(long id) => Children.TryGetValue(id, out long[] c) ? c : [];

        [TestMethod]
        public void NothingIsHiddenInitially()
        {
            HiddenStructureTypeSet set = new();

            Assert.IsTrue(set.IsEmpty);
            Assert.AreEqual(StructureTypeEyeState.Visible, set.StateOf(1, ChildrenOf));
        }

        [TestMethod]
        public void TogglingARootHidesAllDescendants()
        {
            HiddenStructureTypeSet set = new();

            set.Toggle(1, ChildrenOf);

            foreach (long id in new long[] { 1, 2, 3, 4 })
                Assert.IsTrue(set.IsHidden(id), $"type {id} should be hidden");
            Assert.IsFalse(set.IsHidden(5));
            Assert.AreEqual(StructureTypeEyeState.Hidden, set.StateOf(1, ChildrenOf));
        }

        [TestMethod]
        public void TogglingAHiddenRootShowsAllDescendants()
        {
            HiddenStructureTypeSet set = new();
            set.Toggle(1, ChildrenOf);

            set.Toggle(1, ChildrenOf);

            Assert.IsTrue(set.IsEmpty);
        }

        [TestMethod]
        public void TogglingAChildLeavesTheParentMixed()
        {
            HiddenStructureTypeSet set = new();

            set.Toggle(2, ChildrenOf);

            Assert.IsTrue(set.IsHidden(2));
            Assert.IsTrue(set.IsHidden(4));
            Assert.IsFalse(set.IsHidden(1));
            Assert.IsFalse(set.IsHidden(3));
            Assert.AreEqual(StructureTypeEyeState.Mixed, set.StateOf(1, ChildrenOf));
            Assert.AreEqual(StructureTypeEyeState.Hidden, set.StateOf(2, ChildrenOf));
        }

        [TestMethod]
        public void TogglingAMixedParentShowsEverything()
        {
            HiddenStructureTypeSet set = new();
            set.Toggle(2, ChildrenOf);

            set.Toggle(1, ChildrenOf);

            Assert.IsTrue(set.IsEmpty);
        }

        [TestMethod]
        public void ACycleInTheTypeTreeTerminates()
        {
            HiddenStructureTypeSet set = new();
            Dictionary<long, long[]> cyclic = new() { [1] = [2], [2] = [1] };

            set.Toggle(1, id => cyclic.TryGetValue(id, out long[] c) ? c : []);

            Assert.IsTrue(set.IsHidden(1));
            Assert.IsTrue(set.IsHidden(2));
        }

        [TestMethod]
        public void ChangedIsRaisedOnToggleAndClear()
        {
            HiddenStructureTypeSet set = new();
            int raised = 0;
            set.Changed += () => raised++;

            set.Toggle(1, ChildrenOf);
            set.Clear();

            Assert.AreEqual(2, raised);
            Assert.IsTrue(set.IsEmpty);
        }

        [TestMethod]
        public void UnknownTypeIsNotHidden()
        {
            HiddenStructureTypeSet set = new();
            set.Toggle(1, ChildrenOf);

            Assert.IsFalse(set.IsTypeHidden(null));
            Assert.IsFalse(set.IsTypeHidden(99));
            Assert.IsTrue(set.IsTypeHidden(4));
        }
    }
}
