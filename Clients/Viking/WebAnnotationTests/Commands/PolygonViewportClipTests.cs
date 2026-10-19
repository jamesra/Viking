using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.Commands;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Pins <see cref="PolygonViewportClip"/>: the inclusive <c>Covers</c> test that decides when the temporary pen fill
    /// must be re-clipped, and the bounding-box short-circuits and SQL intersection in <c>Clip</c>.
    /// </summary>
    [TestClass]
    public class PolygonViewportClipTests
    {
        private const double AreaTolerance = 1e-6;

        private static Vector2[] Square(double min, double max) =>
            [new(min, min), new(max, min), new(max, max), new(min, max), new(min, min)];

        private static Rectangle Area(double left, double right, double bottom, double top) => new(left, right, bottom, top);

        [TestMethod]
        public void Covers_IdenticalRectangles_IsTrue() =>
            Assert.IsTrue(PolygonViewportClip.Covers(Area(0, 10, 0, 10), Area(0, 10, 0, 10)));

        [TestMethod]
        public void Covers_ViewStrictlyInside_IsTrue() =>
            Assert.IsTrue(PolygonViewportClip.Covers(Area(0, 10, 0, 10), Area(2, 8, 3, 7)));

        [TestMethod]
        public void Covers_ViewTouchingEachEdge_IsTrue()
        {
            Rectangle clip = Area(0, 10, 0, 10);
            Assert.IsTrue(PolygonViewportClip.Covers(clip, Area(0, 5, 2, 8)), "left edge");
            Assert.IsTrue(PolygonViewportClip.Covers(clip, Area(5, 10, 2, 8)), "right edge");
            Assert.IsTrue(PolygonViewportClip.Covers(clip, Area(2, 8, 0, 5)), "bottom edge");
            Assert.IsTrue(PolygonViewportClip.Covers(clip, Area(2, 8, 5, 10)), "top edge");
        }

        [TestMethod]
        public void Covers_ViewExceedingAnySide_IsFalse()
        {
            Rectangle clip = Area(0, 10, 0, 10);
            Assert.IsFalse(PolygonViewportClip.Covers(clip, Area(-1, 5, 2, 8)), "left");
            Assert.IsFalse(PolygonViewportClip.Covers(clip, Area(5, 11, 2, 8)), "right");
            Assert.IsFalse(PolygonViewportClip.Covers(clip, Area(2, 8, -1, 5)), "bottom");
            Assert.IsFalse(PolygonViewportClip.Covers(clip, Area(2, 8, 5, 11)), "top");
        }

        [TestMethod]
        public void Clip_NullPolygon_ReturnsEmpty() =>
            Assert.AreEqual(0, PolygonViewportClip.Clip(null, Area(0, 10, 0, 10)).Count);

        [TestMethod]
        public void Clip_PolygonBeyondEachSide_ReturnsEmpty()
        {
            Rectangle area = Area(0, 10, 0, 10);
            Assert.AreEqual(0, PolygonViewportClip.Clip(new Polygon(Square(-30, -20)), area).Count, "left/below");
            Assert.AreEqual(0, PolygonViewportClip.Clip(new Polygon(Square(20, 30)), area).Count, "right/above");

            Polygon leftOnly = new([new(-30, 2), new(-20, 2), new(-20, 8), new(-30, 8), new(-30, 2)]);
            Polygon rightOnly = new([new(20, 2), new(30, 2), new(30, 8), new(20, 8), new(20, 2)]);
            Polygon belowOnly = new([new(2, -30), new(8, -30), new(8, -20), new(2, -20), new(2, -30)]);
            Polygon aboveOnly = new([new(2, 20), new(8, 20), new(8, 30), new(2, 30), new(2, 20)]);
            Assert.AreEqual(0, PolygonViewportClip.Clip(leftOnly, area).Count, "left");
            Assert.AreEqual(0, PolygonViewportClip.Clip(rightOnly, area).Count, "right");
            Assert.AreEqual(0, PolygonViewportClip.Clip(belowOnly, area).Count, "below");
            Assert.AreEqual(0, PolygonViewportClip.Clip(aboveOnly, area).Count, "above");
        }

        [TestMethod]
        public void Clip_PolygonOnlyTouchingAreaEdge_ReturnsEmpty()
        {
            // Bounding boxes touch, so the SQL path runs and yields a line, which cannot be filled.
            Polygon touching = new(Square(10, 20));
            Assert.AreEqual(0, PolygonViewportClip.Clip(touching, Area(0, 10, 0, 10)).Count);
        }

        [TestMethod]
        public void Clip_PolygonFullyInsideArea_ReturnsSameInstance()
        {
            Polygon inside = new(Square(2, 8));
            List<Polygon> pieces = PolygonViewportClip.Clip(inside, Area(0, 10, 0, 10));
            Assert.AreEqual(1, pieces.Count);
            Assert.AreSame(inside, pieces[0]);
        }

        [TestMethod]
        public void Clip_PolygonBoundsEqualToArea_ReturnsSameInstance()
        {
            Polygon exact = new(Square(0, 10));
            List<Polygon> pieces = PolygonViewportClip.Clip(exact, Area(0, 10, 0, 10));
            Assert.AreEqual(1, pieces.Count);
            Assert.AreSame(exact, pieces[0]);
        }

        [TestMethod]
        public void Clip_PartialOverlap_ReturnsPieceInsideArea()
        {
            Polygon big = new(Square(-50, 50));
            Rectangle area = Area(0, 10, 0, 10);
            List<Polygon> pieces = PolygonViewportClip.Clip(big, area);

            Assert.AreEqual(1, pieces.Count);
            Assert.AreNotSame(big, pieces[0]);
            Rectangle b = pieces[0].BoundingBox;
            Assert.AreEqual(0, b.Left, AreaTolerance);
            Assert.AreEqual(10, b.Right, AreaTolerance);
            Assert.AreEqual(0, b.Bottom, AreaTolerance);
            Assert.AreEqual(10, b.Top, AreaTolerance);
        }

        [TestMethod]
        public void Clip_OverlapAtOneCorner_ReturnsOverlapRectangle()
        {
            Polygon poly = new(Square(5, 20));
            List<Polygon> pieces = PolygonViewportClip.Clip(poly, Area(0, 10, 0, 10));

            Assert.AreEqual(1, pieces.Count);
            Rectangle b = pieces[0].BoundingBox;
            Assert.AreEqual(5, b.Left, AreaTolerance);
            Assert.AreEqual(10, b.Right, AreaTolerance);
            Assert.AreEqual(5, b.Bottom, AreaTolerance);
            Assert.AreEqual(10, b.Top, AreaTolerance);
        }

        [TestMethod]
        public void Clip_UShape_ReturnsTwoPieces()
        {
            // A U whose bottom bar lies below the area: the two arms inside the area are separate pieces.
            Polygon u = new([new(0, -10), new(30, -10), new(30, 20), new(20, 20), new(20, -5), new(10, -5), new(10, 20), new(0, 20), new(0, -10)]);
            List<Polygon> pieces = PolygonViewportClip.Clip(u, Area(-5, 35, 0, 10));

            Assert.AreEqual(2, pieces.Count);
            double[] lefts = pieces.Select(p => p.BoundingBox.Left).OrderBy(x => x).ToArray();
            Assert.AreEqual(0, lefts[0], AreaTolerance);
            Assert.AreEqual(20, lefts[1], AreaTolerance);
            Assert.IsTrue(pieces.All(p => p.BoundingBox.Bottom >= -AreaTolerance && p.BoundingBox.Top <= 10 + AreaTolerance));
        }
    }
}
