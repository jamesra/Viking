using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Combines per-part <see cref="ShapeRelation"/> so mixed inside/outside is Intersecting
    /// (Intersects stays true) and Contains is true only when every part is inside.
    /// </summary>
    internal static class ShapeRelationHelpers
    {
        /// <summary>
        /// One relation for a multi-part shape. A part touching from outside next to a part inside is Intersecting;
        /// parts that are all boundary contact keep <see cref="ShapeRelation.Exterior"/> if any part had it.
        /// </summary>
        public static ShapeRelation CombineParts(IEnumerable<ShapeRelation> parts)
        {
            ShapeRelation combined = ShapeRelation.Contained;
            bool anyHit = false;
            bool anyMiss = false;
            bool anyContained = false;
            bool anyExterior = false;
            foreach (ShapeRelation rel in parts)
            {
                if (rel == ShapeRelation.None)
                {
                    anyMiss = true;
                    continue;
                }

                anyHit = true;
                if (rel == ShapeRelation.Intersecting)
                    return ShapeRelation.Intersecting;
                if ((rel & ~ShapeRelation.Exterior) == ShapeRelation.Touching)
                    combined = ShapeRelation.Touching;
                else
                    anyContained = true;
                anyExterior |= rel.HasFlag(ShapeRelation.Exterior);
            }

            if (!anyHit)
                return ShapeRelation.None;
            if (anyMiss || (anyExterior && anyContained))
                return ShapeRelation.Intersecting;
            return anyExterior ? combined | ShapeRelation.Exterior : combined;
        }

        public static ShapeRelation RelationToCollection(IShape2D self, IShapeCollection2D collection)
        {
            IList<IShape2D> geometries = collection.Geometries;
            if (geometries.Count == 0)
                return ShapeRelation.None;

            List<ShapeRelation> parts = new(geometries.Count);
            foreach (IShape2D g in geometries)
                parts.Add(self.GetRelation(g));

            return CombineParts(parts);
        }

        /// <summary>
        /// Classifies a polyline as the combination of per-segment relations against <paramref name="self"/>.
        /// Avoids converting area shapes to polygons, which can miss segment hits the typed line test reports.
        /// </summary>
        public static ShapeRelation RelationToPolyline(IShape2D self, IPolyLine2D line)
        {
            List<ShapeRelation> parts = new(line.LineSegments.Count);
            foreach (ILineSegment2D seg in line.LineSegments)
                parts.Add(self.GetRelation(seg));
            return CombineParts(parts);
        }

        public static Polygon RectangleAsPolygon(in Rectangle rect) =>
            new([rect.LowerLeft, rect.LowerRight, rect.UpperRight, rect.UpperLeft, rect.LowerLeft]);

        public static Polygon TriangleAsPolygon(in Triangle tri) =>
            new([tri.P1, tri.P2, tri.P3, tri.P1]);

        public static Polygon QuadAsPolygon(in Quad quad) =>
            new([quad.BottomLeft, quad.BottomRight, quad.TopRight, quad.TopLeft, quad.BottomLeft]);
    }
}
