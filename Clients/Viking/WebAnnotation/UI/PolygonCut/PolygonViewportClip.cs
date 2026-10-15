using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System.Collections.Generic;
using VikingXNA;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Cuts a very large polygon down to the part near the visible scene so a temporary fill can be triangulated
    /// quickly. The cut adds flat edges along the clip rectangle. The result is for display only; the pen cut and the
    /// saved shape always use the full polygon.
    /// </summary>
    internal static class PolygonViewportClip
    {
        /// <summary>
        /// Pieces smaller than this (volume pixels squared) are slivers from the clip touching the polygon and are dropped.
        /// </summary>
        private const double MinPieceArea = 1e-6;

        /// <summary>
        /// The volume-space rectangle the temporary fill covers: centered on the view, extended by
        /// <paramref name="marginFactor"/> times the larger view dimension on every side so that small pans, and a
        /// rotated view whose corners reach beyond the axis-aligned bounds, stay covered without re-clipping.
        /// </summary>
        public static Rectangle ClipAreaForScene(Scene scene, double marginFactor = 0.75)
        {
            Vector2 lowerLeft = scene.ScreenToWorld(0, scene.Viewport.Height);
            Vector2 upperRight = scene.ScreenToWorld(scene.Viewport.Width, 0);
            Rectangle visible = new(lowerLeft, upperRight);
            double reach = (System.Math.Max(visible.Width, visible.Height) * 0.5) + (System.Math.Max(visible.Width, visible.Height) * marginFactor);
            Vector2 center = visible.Center;
            return new Rectangle(center.X - reach, center.X + reach, center.Y - reach, center.Y + reach);
        }

        /// <summary>
        /// True when <paramref name="view"/> is still inside <paramref name="clipArea"/>, so a fill clipped to
        /// <paramref name="clipArea"/> still covers everything on screen.
        /// </summary>
        public static bool Covers(Rectangle clipArea, Rectangle view) =>
            clipArea.Left <= view.Left && clipArea.Right >= view.Right && clipArea.Bottom <= view.Bottom && clipArea.Top >= view.Top;

        /// <summary>
        /// Returns the parts of <paramref name="polygon"/> inside <paramref name="area"/>. A concave polygon, or a hole
        /// straddling the border, can give several pieces or an empty list. Returns an empty list when the SQL
        /// intersection rejects the shape, so the caller simply shows no temporary fill.
        /// </summary>
        public static List<Polygon> Clip(Polygon polygon, Rectangle area)
        {
            List<Polygon> pieces = [];
            if (polygon is null)
                return pieces;

            // Skip the SQL round trip when the bounding box already decides the answer.
            Rectangle bounds = polygon.BoundingBox;
            if (bounds.Right < area.Left || bounds.Left > area.Right || bounds.Top < area.Bottom || bounds.Bottom > area.Top)
                return pieces;

            if (Covers(area, bounds))
            {
                pieces.Add(polygon);
                return pieces;
            }

            try
            {
                Vector2[] box = [area.LowerLeft, area.LowerRight, area.UpperRight, area.UpperLeft, area.LowerLeft];
                SqlGeometry clipped = polygon.ToSqlGeometry().STIntersection(box.ToPolygon());
                if (clipped is null || clipped.IsNull || clipped.STIsEmpty().IsTrue)
                    return pieces;

                int count = clipped.STNumGeometries().Value;
                for (int i = 1; i <= count; i++)
                {
                    SqlGeometry part = clipped.STGeometryN(i);
                    // Touching edges produce lines and points; only areas can be filled.
                    if (part.STGeometryType().Value != "Polygon" || part.STArea().Value < MinPieceArea)
                        continue;

                    pieces.Add(part.ToPolygon());
                }
            }
            catch (System.ArgumentException)
            {
                pieces.Clear();
            }
            catch (System.FormatException)
            {
                pieces.Clear();
            }

            return pieces;
        }
    }
}
