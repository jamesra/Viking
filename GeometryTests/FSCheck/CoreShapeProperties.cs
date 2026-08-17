using Geometry;
using System.Linq;

namespace GeometryTests.FSCheck
{
    internal static class CoreShapeProperties
    {
        public static bool IsExclusivePointRelation(ShapeRelation rel) =>
            rel is ShapeRelation.None or ShapeRelation.Contained or ShapeRelation.Touching;

        public static bool ContainsCoversMatchRelation(IShape2D shape, Vector2 p)
        {
            if (shape.ShapeType == ShapeType2D.Collection)
                return true;

            ShapeRelation rel = shape.GetRelation((IPoint2D)p);
            return IsExclusivePointRelation(rel) &&
                   shape.Contains((IPoint2D)p) == rel.IsContains() &&
                   shape.Covers((IPoint2D)p) == rel.IsCovers();
        }

        public static bool BoundingBoxCoversSamples(IShape2D shape)
        {
            if (shape.ShapeType == ShapeType2D.InfiniteLine)
                return double.IsNaN(shape.BoundingBox.Left);

            if (shape is Shape2DCollection collection)
            {
                return collection.Geometries.All(child =>
                    collection.BoundingBox.Covers(child.BoundingBox));
            }

            if (shape is Circle circle)
            {
                Rectangle bb = shape.BoundingBox;
                Vector2 c = circle.Center;
                double r = circle.Radius;
                return bb.Covers(c) &&
                       bb.Covers(c + new Vector2(r, 0)) &&
                       bb.Covers(c + new Vector2(-r, 0)) &&
                       bb.Covers(c + new Vector2(0, r)) &&
                       bb.Covers(c + new Vector2(0, -r));
            }

            if (shape is IHasControlPoints control)
                return control.ControlPoints.All(p => shape.BoundingBox.Covers(p.ToVector2()));

            return true;
        }

        public static bool TranslateRoundTripEquals(IShape2D shape, Vector2 offset)
        {
            if (shape.ShapeType is ShapeType2D.Collection or ShapeType2D.InfiniteLine)
                return true;

            IShape2D back = shape.Translate(offset).Translate(-offset);
            bool box = back.BoundingBox.LowerLeft == shape.BoundingBox.LowerLeft &&
                       back.BoundingBox.UpperRight == shape.BoundingBox.UpperRight;
            if (shape.ShapeType.IsOpen() || shape.ShapeType == ShapeType2D.Point)
                return box;

            return box && Tolerance.AreClose(back.Area, shape.Area);
        }
    }
}
