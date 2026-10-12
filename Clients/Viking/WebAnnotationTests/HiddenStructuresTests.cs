using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI;

namespace WebAnnotationTests
{
    /// <summary>
    /// Tests for the structure set that is hidden while a pen stroke retraces a cell.
    /// </summary>
    [TestClass]
    public class HiddenStructuresTests
    {
        private static readonly Dictionary<long, long[]> NoLinks = [];

        private static HashSet<long> Compute(
            long target,
            Dictionary<long, long[]> children,
            Dictionary<long, long[]> links,
            Dictionary<long, long> parents) =>
            HiddenStructures.Compute(
                target,
                id => children.TryGetValue(id, out long[] c) ? c : [],
                id => links.TryGetValue(id, out long[] l) ? l : [],
                id => parents.TryGetValue(id, out long p) ? p : null);

        [TestMethod]
        public void IncludesChildrenAndGrandchildren()
        {
            Dictionary<long, long[]> children = new() { [1] = [2, 3], [2] = [4] };

            HashSet<long> hidden = Compute(1, children, NoLinks, []);

            CollectionAssert.AreEquivalent(new long[] { 2, 3, 4 }, hidden.ToArray());
        }

        [TestMethod]
        public void TargetWithoutDescendantsHidesNothing()
        {
            // A link from a leaf must not hide anything: with no descendants the stroke keeps waiting for a better target.
            Dictionary<long, long[]> links = new() { [1] = [9] };

            Assert.AreEqual(0, Compute(1, [], links, []).Count);
        }

        [TestMethod]
        public void IncludesStructuresLinkedToDescendants()
        {
            Dictionary<long, long[]> children = new() { [1] = [2] };
            Dictionary<long, long[]> links = new() { [2] = [7, 8] };

            HashSet<long> hidden = Compute(1, children, links, []);

            CollectionAssert.AreEquivalent(new long[] { 2, 7, 8 }, hidden.ToArray());
        }

        [TestMethod]
        public void LinkedStructuresAreNotExpandedFurther()
        {
            // 7 is linked to a descendant; 7's own children and links are not hidden.
            Dictionary<long, long[]> children = new() { [1] = [2], [7] = [70] };
            Dictionary<long, long[]> links = new() { [2] = [7], [7] = [71] };

            HashSet<long> hidden = Compute(1, children, links, []);

            CollectionAssert.AreEquivalent(new long[] { 2, 7 }, hidden.ToArray());
        }

        [TestMethod]
        public void NeverHidesTheTargetOrItsAncestors()
        {
            // 10 -> 1 (target) -> 2, and 2 links to both the target and the target's parent.
            Dictionary<long, long[]> children = new() { [10] = [1], [1] = [2] };
            Dictionary<long, long[]> links = new() { [2] = [1, 10, 5] };
            Dictionary<long, long> parents = new() { [1] = 10, [2] = 1 };

            HashSet<long> hidden = Compute(1, children, links, parents);

            CollectionAssert.AreEquivalent(new long[] { 2, 5 }, hidden.ToArray());
        }

        [TestMethod]
        public void ChildCycleBackToTargetTerminates()
        {
            Dictionary<long, long[]> children = new() { [1] = [2], [2] = [3], [3] = [1, 2] };

            HashSet<long> hidden = Compute(1, children, NoLinks, []);

            CollectionAssert.AreEquivalent(new long[] { 2, 3 }, hidden.ToArray());
        }

        [TestMethod]
        public void HitTestFilterPassesThroughNonLocationAnnotations()
        {
            // Only locations carry a structure; anything else (here a dummy annotation) must never be filtered.
            DummyCanvas canvas = new();
            canvas.Add(new DummyAnnotation(new Circle(new Vector2(0, 0), 10)));
            HiddenStructureHitTestFilter filter = new(canvas, () => new HashSet<long> { 2 });

            Assert.AreEqual(1, filter.GetAnnotations(new Vector2(0, 0)).Count);
            Assert.AreEqual(1, filter.GetAnnotations(new LineSegment(new Vector2(-20, 0), new Vector2(20, 0))).Count);
        }

        [TestMethod]
        public void HitTestFilterToleratesNoHiddenSet()
        {
            DummyCanvas canvas = new();
            canvas.Add(new DummyAnnotation(new Circle(new Vector2(0, 0), 10)));
            HiddenStructureHitTestFilter filter = new(canvas, () => null);

            Assert.AreEqual(1, filter.GetAnnotations(new Vector2(0, 0)).Count);
        }
    }
}
