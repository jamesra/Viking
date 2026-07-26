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
        static GridPolygon RectanglePolygon(GridRectangle r) =>
            new([
                new GridVector2(r.Left, r.Bottom),
                new GridVector2(r.Right, r.Bottom),
                new GridVector2(r.Right, r.Top),
                new GridVector2(r.Left, r.Top),
                new GridVector2(r.Left, r.Bottom),
            ]);

        [TestMethod]
        public void GridRectangle_IntersectsShape2D_DetectsOverlap()
        {
            GridRectangle rect = new(0, 10, 0, 10);
            GridCircle circle = new(new GridVector2(5, 5), 2);

            Assert.IsTrue(rect.Intersects(circle));
        }

        [TestMethod]
        public void GridCircle_ContainsPolygonAndSegment_ReturnTrueWhenInside()
        {
            GridCircle circle = new(new GridVector2(0, 0), 10);
            GridPolygon square = RectanglePolygon(new GridRectangle(-2, 2, -2, 2));
            GridLineSegment segment = new(new GridVector2(-1, 0), new GridVector2(1, 0));

            Assert.IsTrue(circle.Contains(square));
            Assert.IsTrue(circle.Contains(segment));
        }

        [TestMethod]
        public void LineSearchGrid_FindNearest_ReturnsClosestIntersection()
        {
            GridRectangle bounds = new(-10, 10, -10, 10);
            LineSearchGrid<int> grid = new(bounds, 100);
            GridLineSegment target = new(new GridVector2(-5, 0), new GridVector2(5, 0));
            grid.Add(new GridLineSegment(new GridVector2(0, -5), new GridVector2(0, 5)), 1);

            int result = grid.FindNearest(target, out GridVector2 intersection, out double distance);

            Assert.AreEqual(1, result);
            Assert.AreEqual(0, intersection.X, 0.001);
            Assert.AreEqual(0, intersection.Y, 0.001);
            Assert.AreEqual(5, distance, 0.001);
        }

        [TestMethod]
        public void QuadTree_RemoveSingleRootPoint_DoesNotThrow()
        {
            QuadTree<int> tree = new(new GridRectangle(-1, 1, -1, 1));
            GridVector2 point = new(0, 0);
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

            MappingGridVector2[] mapPoints = new MappingGridVector2[gridWidth * gridHeight];
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + (y * gridWidth);
                    GridVector2 mapped = GridTransform.CoordinateFromGridPos(x, y, gridWidth, gridHeight, mappedWidth, mappedHeight);
                    GridVector2 ctrlPoint = new(mapped.X * 0.9 + 10, mapped.Y * 1.1 + 5);
                    mapPoints[i] = new MappingGridVector2(ctrlPoint, mapped);
                }
            }

            GridTransform transform = new(mapPoints, new GridRectangle(0, mappedWidth, 0, mappedHeight), gridWidth, gridHeight, new TransformBasicInfo(DateTime.UtcNow));

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    int i = x + (y * gridWidth);
                    Assert.AreEqual(mapPoints[i].MappedPoint, transform.MapPoints[i].MappedPoint);
                    Assert.AreEqual(mapPoints[i].ControlPoint, transform.MapPoints[i].ControlPoint);
                }
            }

            GridVector2 testMapped = new(mappedWidth * 0.37, mappedHeight * 0.62);
            GridVector2 control = transform.Transform(testMapped);
            GridVector2 roundTrip = transform.InverseTransform(control);

            Assert.IsTrue(transform.CanTransform(testMapped));
            Assert.IsTrue(transform.CanInverseTransform(control));
            Assert.AreEqual(testMapped.X, roundTrip.X, 0.01);
            Assert.AreEqual(testMapped.Y, roundTrip.Y, 0.01);
        }

        [TestMethod]
        public void Delaunay_Triangulate_AcceptsUnsortedInput()
        {
            GridVector2[] points =
            [
                new(5, 0),
                new(1, 1),
                new(3, 2),
                new(0, 4),
                new(4, 3),
            ];
            GridRectangle bounds = points.BoundingBox();

            int[] triangles = Delaunay2D.Triangulate(points, bounds);

            Assert.IsTrue(triangles.Length >= 9);
            Assert.AreEqual(0, triangles.Length % 3);
        }

        [TestMethod]
        public void GridTriangle_Intersects_DetectsCrossingWithoutVertexInside()
        {
            GridTriangle a = new(new GridVector2(0, 0), new GridVector2(4, 0), new GridVector2(0, 4));
            GridTriangle b = new(new GridVector2(2, -2), new GridVector2(2, 2), new GridVector2(6, 2));

            Assert.IsTrue(a.Intersects(b));
        }

        [TestMethod]
        public void GridPolygon_Intersects_DetectsContainedPolygon()
        {
            GridPolygon outer = RectanglePolygon(new GridRectangle(0, 10, 0, 10));
            GridPolygon inner = RectanglePolygon(new GridRectangle(2, 4, 2, 4));

            Assert.IsTrue(outer.Intersects(inner));
            Assert.IsTrue(inner.Intersects(outer));
        }

        [TestMethod]
        public void GridVector2_GetHashCode_DistinguishesDistinctPoints()
        {
            GridVector2 a = new(1, 2);
            GridVector2 b = new(2, 1);

            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void GridVector2_GetHashCode_MatchesForEpsilonEqualPoints()
        {
            GridVector2 a = new(1.0, 2.0);
            GridVector2 b = new(1.0 + Geometry.Global.Epsilon * 0.1, 2.0);

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void GridLineSegment_GetHashCode_IsUndirectedAndEpsilonStable()
        {
            GridLineSegment ab = new(new GridVector2(0, 0), new GridVector2(10, 0));
            GridLineSegment ba = new(new GridVector2(10, 0), new GridVector2(0, 0));
            GridLineSegment epsilonEqual = new(
                new GridVector2(Geometry.Global.Epsilon * 0.1, 0),
                new GridVector2(10 - Geometry.Global.Epsilon * 0.1, 0));

            Assert.AreEqual(ab, ba);
            Assert.AreEqual(ab.GetHashCode(), ba.GetHashCode());
            Assert.AreEqual(ab, epsilonEqual);
            Assert.AreEqual(ab.GetHashCode(), epsilonEqual.GetHashCode());
        }

        [TestMethod]
        public void GridRectangle_GetHashCode_DistinguishesDifferentExtents()
        {
            GridRectangle a = new(new GridVector2(0, 0), new GridVector2(10, 10));
            GridRectangle b = new(new GridVector2(0, 0), new GridVector2(20, 10));

            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void GridCircle_GetHashCode_MatchesForEqualCircles()
        {
            GridCircle a = new(new GridVector2(1, 2), 3);
            GridCircle b = new(new GridVector2(1, 2), 3);

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void GridTriangle_GetHashCode_MatchesForEqualTriangles()
        {
            GridTriangle a = new(new GridVector2(0, 0), new GridVector2(4, 0), new GridVector2(0, 4));
            GridTriangle b = new(new GridVector2(0, 0), new GridVector2(4, 0), new GridVector2(0, 4));

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
            GridPolygon outer = RectanglePolygon(new GridRectangle(-10, 10, -10, 10));
            GridPolygon inner = RectanglePolygon(new GridRectangle(-2, 2, -2, 2));

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
            GridVector2 offset = new(61000, -72000);

            GridPolygon a = RectanglePolygon(new GridRectangle(-50, 50, -50, 50)).Translate(offset);
            GridPolygon b = RectanglePolygon(new GridRectangle(0, 100, 0, 100)).Translate(offset);

            List<GridVector2> intersections = a.AddPointsAtIntersections(b);

            Assert.IsTrue(a.IsValid(), "Polygon A must remain valid after inserting corresponding points.");
            Assert.IsTrue(b.IsValid(), "Polygon B must remain valid after inserting corresponding points.");

            //The two rectangles cross at (50, 0) and (0, 50) relative to the offset.
            Assert.IsTrue(intersections.Count >= 2, "Expected the overlapping rectangle edges to produce corresponding points.");

            foreach (GridVector2 p in intersections)
            {
                Assert.IsTrue(a.IsVertex(p), $"Intersection {p} should be a vertex of polygon A.");
                Assert.IsTrue(b.IsVertex(p), $"Intersection {p} should be a vertex of polygon B.");
            }
        }

        [TestMethod]
        public void QuadTreeWithUniqueValues_UpdateValue_RefreshesReverseLookup()
        {
            QuadTreeWithUniqueValues<string> tree = new(new GridRectangle(-10, 10, -10, 10));
            GridVector2 point = new(1, 2);
            tree.Add(point, "old");

            tree.Update(point, "new");

            Assert.AreEqual(point, tree["new"]);
            Assert.ThrowsException<KeyNotFoundException>(() => _ = tree["old"]);
        }
    }
}
