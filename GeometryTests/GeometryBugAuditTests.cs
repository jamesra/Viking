using Geometry;
using Geometry.Meshing;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace GeometryTests
{
    [TestClass]
    public class GeometryBugAuditTests
    {
        static Polygon RectanglePolygon(Rectangle r) =>
            new([
                new Vector2(r.Left, r.Bottom),
                new Vector2(r.Right, r.Bottom),
                new Vector2(r.Right, r.Top),
                new Vector2(r.Left, r.Top),
                new Vector2(r.Left, r.Bottom),
            ]);

        [TestMethod]
        public void GridRectangle_IntersectsShape2D_DetectsOverlap()
        {
            Rectangle rect = new(0, 10, 0, 10);
            Circle circle = new(new Vector2(5, 5), 2);

            Assert.IsTrue(rect.Intersects(circle));
        }

        [TestMethod]
        public void GridCircle_ContainsPolygonAndSegment_ReturnTrueWhenInside()
        {
            Circle circle = new(new Vector2(0, 0), 10);
            Polygon square = RectanglePolygon(new Rectangle(-2, 2, -2, 2));
            LineSegment segment = new(new Vector2(-1, 0), new Vector2(1, 0));

            Assert.IsTrue(circle.Contains(square));
            Assert.IsTrue(circle.Contains(segment));
        }

        [TestMethod]
        public void LineSearchGrid_FindNearest_ReturnsClosestIntersection()
        {
            Rectangle bounds = new(-10, 10, -10, 10);
            LineSearchGrid<int> grid = new(bounds, 100);
            LineSegment target = new(new Vector2(-5, 0), new Vector2(5, 0));
            grid.Add(new LineSegment(new Vector2(0, -5), new Vector2(0, 5)), 1);

            int result = grid.FindNearest(target, out Vector2 intersection, out double distance);

            Assert.AreEqual(1, result);
            Assert.AreEqual(0, intersection.X, 0.001);
            Assert.AreEqual(0, intersection.Y, 0.001);
            Assert.AreEqual(5, distance, 0.001);
        }

        [TestMethod]
        public void QuadTree_RemoveSingleRootPoint_DoesNotThrow()
        {
            QuadTree<int> tree = new(new Rectangle(-1, 1, -1, 1));
            Vector2 point = new(0, 0);
            tree.Add(point, 42);

            tree.TryRemove(point, out int removed);

            Assert.AreEqual(42, removed);
            Assert.IsFalse(tree.Contains(point));
        }

        [TestMethod]
        public void GridTransform_NonSquareGrid_PreservesPointOrderAndRoundTrips()
        {
            const int gridWidth = 4;
            const int gridHeight = 3;
            const double mappedWidth = 300;
            const double mappedHeight = 200;

            MappingVector2[] mapPoints = new MappingVector2[gridWidth * gridHeight];
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + (y * gridWidth);
                    Vector2 mapped = GridTransform.CoordinateFromGridPos(x, y, gridWidth, gridHeight, mappedWidth, mappedHeight);
                    Vector2 ctrlPoint = new(mapped.X * 0.9 + 10, mapped.Y * 1.1 + 5);
                    mapPoints[i] = new MappingVector2(ctrlPoint, mapped);
                }
            }

            GridTransform transform = new(mapPoints, new Rectangle(0, mappedWidth, 0, mappedHeight), gridWidth, gridHeight, new TransformBasicInfo(DateTime.UtcNow));

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + (y * gridWidth);
                    Assert.AreEqual(mapPoints[i].MappedPoint, transform.MapPoints[i].MappedPoint);
                    Assert.AreEqual(mapPoints[i].ControlPoint, transform.MapPoints[i].ControlPoint);
                }
            }

            Vector2 testMapped = new(mappedWidth * 0.37, mappedHeight * 0.62);
            Vector2 control = transform.Transform(testMapped);
            Vector2 roundTrip = transform.InverseTransform(control);

            Assert.IsTrue(transform.CanTransform(testMapped));
            Assert.IsTrue(transform.CanInverseTransform(control));
            Assert.AreEqual(testMapped.X, roundTrip.X, 0.01);
            Assert.AreEqual(testMapped.Y, roundTrip.Y, 0.01);
        }

        [TestMethod]
        public void Delaunay_Triangulate_AcceptsUnsortedInput()
        {
            Vector2[] points =
            [
                new(5, 0),
                new(1, 1),
                new(3, 2),
                new(0, 4),
                new(4, 3),
            ];
            Rectangle bounds = points.BoundingBox();

            int[] triangles = Delaunay2D.Triangulate(points, bounds);

            Assert.IsTrue(triangles.Length >= 9);
            Assert.AreEqual(0, triangles.Length % 3);
        }

        [TestMethod]
        public void GridTriangle_Intersects_DetectsCrossingWithoutVertexInside()
        {
            Triangle a = new(new Vector2(0, 0), new Vector2(4, 0), new Vector2(0, 4));
            Triangle b = new(new Vector2(2, -2), new Vector2(2, 2), new Vector2(6, 2));

            Assert.IsTrue(a.Intersects(b));
        }

        [TestMethod]
        public void GridPolygon_Intersects_DetectsContainedPolygon()
        {
            Polygon outer = RectanglePolygon(new Rectangle(0, 10, 0, 10));
            Polygon inner = RectanglePolygon(new Rectangle(2, 4, 2, 4));

            Assert.IsTrue(outer.Intersects(inner));
            Assert.IsTrue(inner.Intersects(outer));
        }

        [TestMethod]
        public void GridVector2_GetHashCode_DistinguishesDistinctPoints()
        {
            Vector2 a = new(1, 2);
            Vector2 b = new(2, 1);

            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void GridVector2_GetHashCode_MatchesForEpsilonEqualPoints()
        {
            Vector2 a = new(1.0, 2.0);
            Vector2 b = new(1.0 + Geometry.Global.Epsilon * 0.1, 2.0);

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void GridLineSegment_GetHashCode_IsDirectedAndEpsilonStable()
        {
            LineSegment ab = new(new Vector2(0, 0), new Vector2(10, 0));
            LineSegment ba = new(new Vector2(10, 0), new Vector2(0, 0));
            LineSegment epsilonEqual = new(
                new Vector2(Geometry.Global.Epsilon * 0.1, 0),
                new Vector2(10 - Geometry.Global.Epsilon * 0.1, 0));

            Assert.AreNotEqual(ab, ba);
            Assert.IsTrue(ab.EquivalentUndirected(ba));
            Assert.AreNotEqual(ab.GetHashCode(), ba.GetHashCode());
            Assert.AreEqual(ab, epsilonEqual);
            Assert.AreEqual(ab.GetHashCode(), epsilonEqual.GetHashCode());
        }

        [TestMethod]
        public void GridRectangle_GetHashCode_DistinguishesDifferentExtents()
        {
            Rectangle a = new(new Vector2(0, 0), new Vector2(10, 10));
            Rectangle b = new(new Vector2(0, 0), new Vector2(20, 10));

            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void GridCircle_GetHashCode_MatchesForEqualCircles()
        {
            Circle a = new(new Vector2(1, 2), 3);
            Circle b = new(new Vector2(1, 2), 3);

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void GridTriangle_GetHashCode_MatchesForEqualTriangles()
        {
            Triangle a = new(new Vector2(0, 0), new Vector2(4, 0), new Vector2(0, 4));
            Triangle b = new(new Vector2(0, 0), new Vector2(4, 0), new Vector2(0, 4));

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void Color_GetHashCode_PacksArgbComponents()
        {
            Geometry.Graphics.Color a = new(10, 20, 30, 40);
            Geometry.Graphics.Color b = new(10, 20, 30, 40);
            Geometry.Graphics.Color c = new(11, 20, 30, 40);

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a.GetHashCode(), c.GetHashCode());
        }

        [TestMethod]
        public void AddInteriorRing_ValidHole_Succeeds()
        {
            Polygon outer = RectanglePolygon(new Rectangle(-10, 10, -10, 10));
            Polygon inner = RectanglePolygon(new Rectangle(-2, 2, -2, 2));

            outer.AddInteriorRing(inner);

            Assert.AreEqual(1, outer.InteriorRings.Count);
            Assert.IsTrue(outer.Area > 0);
        }

        [TestMethod]
        public void AddPointsAtIntersections_LargeCoordinates_SharesVerticesAndStaysValid()
        {
            //Reproduces the slice-graph correspondence scenario from the Bajaj mesh log: two overlapping
            //polygons positioned at large coordinates (~61000, -72000) where the fixed epsilon (0.001) is tiny
            //relative to the magnitude.  AddPointsAtIntersections must insert the intersection points on both
            //polygons (so adjacent slices share corresponding vertices) and leave both polygons valid.
            Vector2 offset = new(61000, -72000);

            Polygon a = RectanglePolygon(new Rectangle(-50, 50, -50, 50)).Translate(offset);
            Polygon b = RectanglePolygon(new Rectangle(0, 100, 0, 100)).Translate(offset);

            List<Vector2> intersections = a.AddPointsAtIntersections(b);

            Assert.IsTrue(a.IsValid(), "Polygon A must remain valid after inserting corresponding points.");
            Assert.IsTrue(b.IsValid(), "Polygon B must remain valid after inserting corresponding points.");

            //The two rectangles cross at (50, 0) and (0, 50) relative to the offset.
            Assert.IsTrue(intersections.Count >= 2, "Expected the overlapping rectangle edges to produce corresponding points.");

            foreach (Vector2 p in intersections)
            {
                Assert.IsTrue(a.IsVertex(p), $"Intersection {p} should be a vertex of polygon A.");
                Assert.IsTrue(b.IsVertex(p), $"Intersection {p} should be a vertex of polygon B.");
            }
        }

        [TestMethod]
        public void QuadTreeWithUniqueValues_UpdateValue_RefreshesReverseLookup()
        {
            QuadTreeWithUniqueValues<string> tree = new(new Rectangle(-10, 10, -10, 10));
            Vector2 point = new(1, 2);
            tree.Add(point, "old");

            tree.Update(point, "new");

            Assert.AreEqual(point, tree["new"]);
            Assert.ThrowsException<KeyNotFoundException>(() => _ = tree["old"]);
        }
    }
}
