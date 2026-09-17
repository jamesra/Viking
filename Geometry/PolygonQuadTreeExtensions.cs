using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// QuadTree lookups from polygon vertices. Lives in repo Geometry because QuadTree is not part of Core.
    /// </summary>
    public static class PolygonQuadTreeExtensions
    {
        /// <summary>
        /// Maps each vertex of this polygon to a <see cref="PolygonIndex"/>, keeping one index when vertices coincide.
        /// </summary>
        public static QuadTreeWithUniqueValues<PolygonIndex> CreatePointToPolyMap(this Polygon polygon)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));

            QuadTreeWithUniqueValues<List<PolygonIndex>> map = CreatePointToPolyMap([polygon]);
            QuadTreeWithUniqueValues<PolygonIndex> flatMap = new();

            foreach (Vector2 p in map.Keys)
                flatMap.Add(p, map[p].First());

            return flatMap;
        }

        /// <summary>
        /// Maps vertices to polygon indexes when polygons may not share vertices.
        /// </summary>
        public static QuadTreeWithUniqueValues<PolygonIndex> CreatePointToPolyMap2D(this Polygon[] polygons)
        {
            if (polygons is null)
                throw new ArgumentNullException(nameof(polygons));

            QuadTreeWithUniqueValues<PolygonIndex> pointToPoly = new();
            for (int iPoly = 0; iPoly < polygons.Length; iPoly++)
            {
                Polygon poly = polygons[iPoly];

                for (int iVertex = 0; iVertex < poly.ExteriorRing.Length - 1; iVertex++)
                {
                    Vector2 p = poly.ExteriorRing[iVertex];
                    PolygonIndex value = new(iPoly, iVertex, polygons);

                    if (pointToPoly.ContainsKey(p))
                        throw new ArgumentException($"Duplicate vertex {p}");

                    pointToPoly.Add(p, value);
                }

                for (int iInnerPoly = 0; iInnerPoly < poly.InteriorPolygons.Count; iInnerPoly++)
                {
                    Polygon innerPolygon = poly.InteriorPolygons.ElementAt(iInnerPoly);

                    for (int iVertex = 0; iVertex < innerPolygon.ExteriorRing.Length - 1; iVertex++)
                    {
                        Vector2 p = innerPolygon.ExteriorRing[iVertex];
                        PolygonIndex value = new(iPoly, iInnerPoly, iVertex, polygons);
                        if (pointToPoly.ContainsKey(p))
                            throw new ArgumentException($"Duplicate inner polygon vertex {p}");

                        pointToPoly.Add(p, value);
                    }
                }
            }

            return pointToPoly;
        }

        /// <summary>
        /// Maps vertices to polygon indexes when polygons may share vertices.
        /// </summary>
        public static QuadTreeWithUniqueValues<List<PolygonIndex>> CreatePointToPolyMap(this Polygon[] polygons, IReadOnlyList<int> polygonIndices = null)
        {
            if (polygons is null)
                throw new ArgumentNullException(nameof(polygons));

            QuadTreeWithUniqueValues<List<PolygonIndex>> pointToPoly = new();
            for (int iPoly = 0; iPoly < polygons.Length; iPoly++)
            {
                int iPolygon = polygonIndices is null ? iPoly : polygonIndices[iPoly];

                Polygon poly = polygons[iPoly];
                for (int iVertex = 0; iVertex < poly.ExteriorRing.Length - 1; iVertex++)
                {
                    Vector2 p = poly.ExteriorRing[iVertex];
                    PolygonIndex value = new(iPolygon, iVertex, polygons);

                    if (pointToPoly.TryGetValue(p, out List<PolygonIndex> existing))
                        existing.Add(value);
                    else
                        pointToPoly.Add(p, [value]);
                }

                for (int iInnerPoly = 0; iInnerPoly < poly.InteriorPolygons.Count; iInnerPoly++)
                {
                    Polygon innerPolygon = poly.InteriorPolygons.ElementAt(iInnerPoly);

                    for (int iVertex = 0; iVertex < innerPolygon.ExteriorRing.Length - 1; iVertex++)
                    {
                        Vector2 p = innerPolygon.ExteriorRing[iVertex];
                        PolygonIndex value = new(iPolygon, iInnerPoly, iVertex, polygons);
                        if (pointToPoly.TryGetValue(p, out List<PolygonIndex> existing))
                            existing.Add(value);
                        else
                            pointToPoly.Add(p, [value]);
                    }
                }
            }

            return pointToPoly;
        }
    }
}
