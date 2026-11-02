using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotation.UI.CellOverlap
{
    /// <summary>
    /// Where two cells' outlines overlap on one section: the polygon of the shared area and the two locations whose
    /// outlines cross there. Volume space, like every shape the views draw (<c>annotation-volume-geometry.mdc</c>).
    /// </summary>
    internal sealed record CellOverlapRegion(long LocationIdA, long LocationIdB, Polygon Shape)
    {
        /// <summary>The other location of the pair, or null when <paramref name="locationId"/> is neither.</summary>
        public long? Other(long locationId) =>
            locationId == LocationIdA ? LocationIdB : locationId == LocationIdB ? LocationIdA : null;
    }

    /// <summary>The boolean geometry behind the overlap shading and the assign action. No store, no UI.</summary>
    internal static class CellOverlapGeometry
    {
        /// <summary>
        /// Smaller overlaps are not reported: outlines that only touch, or share a pen-traced edge, leave slivers of
        /// this size (volume pixels squared) that are not worth shading.
        /// </summary>
        public const double MinRegionArea = 4.0;

        /// <summary>The parts of <paramref name="a"/> and <paramref name="b"/>'s shared area larger than <see cref="MinRegionArea"/>.</summary>
        public static IReadOnlyList<Polygon> Intersection(Polygon a, Polygon b)
        {
            if (a is null || b is null || !a.BoundingBox.Intersects(b.BoundingBox) || !a.Intersects(b))
                return [];

            try
            {
                SqlGeometry first = AutoPolygonizeSelection.EnsureValid(a.ToSqlGeometry());
                SqlGeometry second = AutoPolygonizeSelection.EnsureValid(b.ToSqlGeometry());
                if (first is null || first.IsNull || second is null || second.IsNull)
                    return [];

                return [.. Parts(first.STIntersection(second)).Where(part => part.Area > MinRegionArea)];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cell overlap intersection failed: {ex.Message}");
                return [];
            }
        }

        /// <summary>
        /// <paramref name="outline"/> without <paramref name="region"/>. The part that covers <paramref name="keepPoint"/>
        /// (else the largest) is <paramref name="primary"/>; every other part is returned in <paramref name="extras"/>
        /// so the caller can keep it as a new location instead of losing that area.
        /// </summary>
        /// <returns>
        /// False when the subtraction failed or nothing is left (the region covers the whole outline); the outputs are
        /// then empty.
        /// </returns>
        public static bool TrySubtract(Polygon outline, Polygon region, Vector2 keepPoint, out Polygon? primary, out IReadOnlyList<Polygon> extras)
        {
            primary = null;
            extras = [];
            if (outline is null || region is null)
                return false;

            try
            {
                SqlGeometry from = AutoPolygonizeSelection.EnsureValid(outline.ToSqlGeometry());
                SqlGeometry take = AutoPolygonizeSelection.EnsureValid(region.ToSqlGeometry());
                if (from is null || from.IsNull || take is null || take.IsNull)
                    return false;

                List<Polygon> parts = [.. Parts(AutoPolygonizeSelection.EnsureValid(from.STDifference(take)))
                    .Where(part => part.Area > CellOverlapGeometry.MinRegionArea)];
                if (parts.Count == 0)
                    return false;

                Polygon chosen = parts.FirstOrDefault(part => part.Covers(keepPoint)) ?? parts.OrderByDescending(part => part.Area).First();
                primary = chosen;
                extras = [.. parts.Where(part => !ReferenceEquals(part, chosen))];
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cell overlap subtraction failed: {ex.Message}");
                primary = null;
                extras = [];
                return false;
            }
        }

        private static IEnumerable<Polygon> Parts(SqlGeometry geometry)
        {
            foreach (SqlGeometry part in AutoPolygonizeSelection.EnumeratePolygons(geometry))
            {
                Polygon? polygon = null;
                try
                {
                    polygon = part.ToPolygon();
                }
                catch (ArgumentException)
                {
                }

                if (polygon is not null)
                    yield return polygon;
            }
        }
    }
}
