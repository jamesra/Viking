using System;
using System.Collections.Generic;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;
using WebAnnotationModel.Objects;
using Color = Microsoft.Xna.Framework.Color;

namespace WebAnnotation.Tools
{
    /// <summary>
    /// Line drawing shared by the tool previews: polylines, closed rings, and a location's outline, in volume coordinates.
    /// Widths are in screen pixels and scaled by the downsample so the feedback keeps its size as the user zooms.
    /// </summary>
    static class ToolPreviewDrawing
    {
        const int CircleSegments = 48;

        public static readonly Color HoverColor = new(255, 255, 255, 150);
        public static readonly Color ActionColor = new(255, 255, 0, 220);

        /// <summary>Line segments joining <paramref name="points"/>, closing the ring when <paramref name="closed"/> is set.</summary>
        public static void AddPath(List<LineView> lines, IReadOnlyList<Vector2> points, bool closed, double width, Color color, LineStyle style = LineStyle.Standard)
        {
            if (points == null || points.Count < 2)
                return;

            for (int i = 1; i < points.Count; i++)
                lines.Add(new LineView(points[i - 1], points[i], width, color, style));
            if (closed && points.Count > 2)
                lines.Add(new LineView(points[points.Count - 1], points[0], width, color, style));
        }

        /// <summary>A circle drawn as a closed ring of short segments, so it outlines without covering what is inside.</summary>
        public static void AddRing(List<LineView> lines, Vector2 center, double radius, double width, Color color)
        {
            if (radius <= 0)
                return;

            Vector2[] ring = new Vector2[CircleSegments];
            for (int i = 0; i < CircleSegments; i++)
            {
                double angle = 2 * Math.PI * i / CircleSegments;
                ring[i] = new Vector2(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
            }

            AddPath(lines, ring, closed: true, width, color);
        }

        /// <summary>
        /// The outline of <paramref name="location"/> as its view draws it, from the forward owner
        /// (<see cref="LocationVolumeGeometryExtensions.VolumeGeometry"/>), never the stored volume cache: a circle's ring,
        /// a polygon's or closed curve's outline, a line's drawn path. Nothing when the location cannot be mapped.
        /// </summary>
        public static void AddOutline(List<LineView> lines, LocationObj location, double width, Color color)
        {
            if (location?.VolumeGeometry() is not Viking.VolumeModel.LocationVolumeGeometry geometry)
                return;

            if (geometry.Circle is Circle circle)
                AddRing(lines, circle.Center, circle.Radius, width, color);
            else if (geometry.Outline is Microsoft.SqlServer.Types.SqlGeometry outline)
                AddPath(lines, SqlGeometryUtils.Extensions.ToPoints(outline), closed: true, width, color);
            else
                AddPath(lines, SqlGeometryUtils.Extensions.ToPoints(geometry.Shape), closed: false, width, color);
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, List<LineView> lines)
        {
            if (lines.Count > 0)
                LineView.Draw(device, scene, lineManager, lines.ToArray());
        }

    }
}
