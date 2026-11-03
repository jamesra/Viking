#nullable enable

using System.Collections.Generic;
using System.Linq;
using Geometry;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Merges the polygons of one segmentation answer that overlap, so a painted object the server returned in pieces (or two painted
    /// objects that ended up touching) is shown and accepted as one polygon. Reuses auto-polygonize's overlap test and union.
    /// </summary>
    internal static class SegmentationResultMerge
    {
        /// <summary>
        /// The polygons with every group of overlapping ones replaced by its union. Polygons that only touch from outside, or do not
        /// meet, stay separate. A group whose union fails is left as it was. Order follows each group's first member.
        /// </summary>
        /// <param name="polygons">One answer's polygons, volume space.</param>
        /// <param name="keepPoints">
        /// Points known to lie on the object (the foreground clicks). When a union splits into parts, the part holding one of them is kept.
        /// </param>
        public static IReadOnlyList<Polygon> MergeOverlapping(IReadOnlyList<Polygon> polygons, IReadOnlyList<Vector2>? keepPoints = null)
        {
            if (polygons is null || polygons.Count < 2)
                return polygons ?? [];

            int[] group = new int[polygons.Count];
            for (int i = 0; i < group.Length; i++)
                group[i] = i;

            int Find(int i)
            {
                while (group[i] != i)
                    i = group[i] = group[group[i]];

                return i;
            }

            for (int i = 0; i < polygons.Count; i++)
            {
                for (int j = i + 1; j < polygons.Count; j++)
                {
                    if (Find(i) != Find(j) && AutoPolygonizeSelection.ShouldGroupSiblingPolygons(polygons[i], polygons[j]))
                        group[Find(j)] = Find(i);
                }
            }

            List<Polygon> merged = [];
            foreach (IGrouping<int, int> members in Enumerable.Range(0, polygons.Count).GroupBy(Find).OrderBy(g => g.Min()))
            {
                List<Polygon> parts = [.. members.Select(index => polygons[index])];
                if (parts.Count == 1)
                {
                    merged.Add(parts[0]);
                    continue;
                }

                Vector2? keep = keepPoints?.Where(point => parts.Any(part => part.Covers(point))).Select(point => (Vector2?)point).FirstOrDefault();
                Polygon? union = AutoPolygonizeSelection.UnionPolygons(parts, keep);
                if (union is null)
                    merged.AddRange(parts);
                else
                    merged.Add(union);
            }

            return merged;
        }
    }
}
