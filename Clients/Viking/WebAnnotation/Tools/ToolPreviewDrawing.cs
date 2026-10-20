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

        /// <summary>The outline of <paramref name="location"/>'s volume shape; nothing if the shape is not loaded.</summary>
        public static void AddOutline(List<LineView> lines, LocationObj location, double width, Color color)
        {
            switch (location?.VolumeShape)
            {
                case ICircle2D circle:
                    AddRing(lines, new Vector2(circle.Center.X, circle.Center.Y), circle.Radius, width, color);
                    break;
                case IPolygon2D polygon:
                    AddPath(lines, ToVectors(polygon.ExteriorRing), closed: true, width, color);
                    break;
                case IPolyLine2D polyline:
                    AddPath(lines, ToVectors(polyline.Points), closed: false, width, color);
                    break;
            }
        }

        public static void Draw(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, List<LineView> lines)
        {
            if (lines.Count > 0)
                LineView.Draw(device, scene, lineManager, lines.ToArray());
        }

        static Vector2[] ToVectors(IReadOnlyList<IPoint2D> points)
        {
            if (points == null)
                return null;

            Vector2[] result = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
                result[i] = new Vector2(points[i].X, points[i].Y);
            return result;
        }
    }
}
