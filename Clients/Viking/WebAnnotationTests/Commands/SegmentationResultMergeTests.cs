using System.Linq;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Pins how the paintbrush merges the polygons of one segmentation answer: overlapping ones become one, others are left alone.
    /// </summary>
    [TestClass]
    public class SegmentationResultMergeTests
    {
        private static Polygon Square(double x, double y, double size) => new(
            new[] { new Vector2(x, y), new Vector2(x + size, y), new Vector2(x + size, y + size), new Vector2(x, y + size), new Vector2(x, y) });

        [TestMethod]
        public void OverlappingPolygons_BecomeOne()
        {
            var merged = SegmentationResultMerge.MergeOverlapping([Square(0, 0, 10), Square(5, 0, 10)]);

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual(150, merged[0].Area, 1e-6, "10x10 and 10x10 overlapping by 5x10");
        }

        [TestMethod]
        public void SeparatePolygons_AreLeftAlone()
        {
            var first = Square(0, 0, 10);
            var second = Square(50, 0, 10);

            var merged = SegmentationResultMerge.MergeOverlapping([first, second]);

            Assert.AreEqual(2, merged.Count);
            Assert.AreSame(first, merged[0]);
            Assert.AreSame(second, merged[1]);
        }

        [TestMethod]
        public void Chain_AOverlapsB_BOverlapsC_BecomesOne()
        {
            var merged = SegmentationResultMerge.MergeOverlapping([Square(0, 0, 10), Square(40, 0, 10), Square(8, 0, 36)]);

            Assert.AreEqual(1, merged.Count);
        }

        [TestMethod]
        public void OneOverlappingPairAndOneOutsider_GiveTwo()
        {
            var merged = SegmentationResultMerge.MergeOverlapping([Square(0, 0, 10), Square(50, 0, 10), Square(5, 5, 10)]);

            Assert.AreEqual(2, merged.Count);
            Assert.IsTrue(merged.Any(p => System.Math.Abs(p.Area - 100) < 1e-6), "the outsider is unchanged");
        }

        [TestMethod]
        public void NullEmptyAndSingle_PassThrough()
        {
            Assert.AreEqual(0, SegmentationResultMerge.MergeOverlapping(null).Count);
            Assert.AreEqual(0, SegmentationResultMerge.MergeOverlapping([]).Count);
            var only = Square(0, 0, 10);
            Assert.AreSame(only, SegmentationResultMerge.MergeOverlapping([only])[0]);
        }
    }
}
