#nullable enable

using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using ProtoCircle = Viking.gRPC.Geometry.V1.Circle;
using ProtoCurveFit = Viking.gRPC.Geometry.V1.CurveFit;
using ProtoCurveMethod = Viking.gRPC.Geometry.V1.CurveMethod;
using ProtoLine = Viking.gRPC.Geometry.V1.Line;
using ProtoPolygon = Viking.gRPC.Geometry.V1.Polygon;
using ProtoRing = Viking.gRPC.Geometry.V1.Ring;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;
using ProtoVector2 = Viking.gRPC.Geometry.V1.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Prompt geometry as <c>viking.geometry.v1</c> shapes in the frame of a request's tiles: the tiles' space (mosaic or
    /// volume, the space the shapes are already in) divided by the grid's downsample, X right, Y up.
    /// </summary>
    /// <remarks>
    /// Shapes are sent as they are; the server encodes them for its model. A circle goes with its true radius in the tiles'
    /// frame (a volume circle's radius was carried by the five-point mean scale when it was mapped, see
    /// <c>.cursor/rules/circle-mapping-scale.mdc</c>), never scaled by the model's <c>circle_diameter_scale</c>, which
    /// the server applies. Rings on the wire do not repeat their first vertex.
    /// </remarks>
    internal static class SegmentationPromptShapes
    {
        /// <summary>A point of the tiles' space in the grid frame at <paramref name="downsample"/>.</summary>
        public static ProtoVector2 ToGrid(Vector2 point, int downsample) =>
            new() { X = point.X / downsample, Y = point.Y / downsample };

        /// <summary>A circle with its true radius, both in the grid frame at <paramref name="downsample"/>.</summary>
        public static ProtoShape Circle(Circle circle, int downsample) => new()
        {
            Circle = new ProtoCircle { Center = ToGrid(circle.Center, downsample), Radius = circle.Radius / downsample }
        };

        /// <summary>
        /// A polygon from its exterior ring followed by its holes. <paramref name="fit"/> null draws straight segments; a
        /// curve polygon passes its control points and the fit.
        /// </summary>
        /// <returns>Null when the exterior has fewer than three distinct vertices.</returns>
        public static ProtoShape? Polygon(IReadOnlyList<Vector2[]> rings, int downsample, ProtoCurveFit? fit = null)
        {
            if (rings is null || rings.Count == 0)
                return null;

            ProtoRing? exterior = Ring(rings[0], downsample);
            if (exterior is null)
                return null;

            ProtoPolygon polygon = new() { Exterior = exterior };
            foreach (Vector2[] hole in rings.Skip(1))
            {
                if (Ring(hole, downsample) is ProtoRing ring)
                    polygon.Holes.Add(ring);
            }

            if (fit is not null)
                polygon.Fit = fit;

            return new ProtoShape { Polygon = polygon };
        }

        /// <summary>An axis-aligned rectangle as a four-vertex polygon.</summary>
        public static ProtoShape? Rectangle(Rectangle rectangle, int downsample) => Polygon(
            [[
                new Vector2(rectangle.Left, rectangle.Bottom),
                new Vector2(rectangle.Right, rectangle.Bottom),
                new Vector2(rectangle.Right, rectangle.Top),
                new Vector2(rectangle.Left, rectangle.Top),
            ]],
            downsample);

        /// <summary>
        /// A line drawn <paramref name="width"/> wide (the tiles' units, carried into the tiles' space by the mean scale).
        /// A closed line drops a repeated closing vertex.
        /// </summary>
        /// <returns>Null when fewer than two points remain.</returns>
        public static ProtoShape? Line(IReadOnlyList<Vector2> points, bool closed, double width, int downsample, ProtoCurveFit? fit = null)
        {
            if (points is null)
                return null;

            List<Vector2> kept = [.. points];
            if (closed && kept.Count > 1 && kept[0] == kept[kept.Count - 1])
                kept.RemoveAt(kept.Count - 1);

            if (kept.Count < 2)
                return null;

            ProtoLine line = new() { Closed = closed, Width = Math.Max(0, width) / downsample };
            foreach (Vector2 point in kept)
                line.Points.Add(ToGrid(point, downsample));

            if (fit is not null)
                line.Fit = fit;

            return new ProtoShape { Line = line };
        }

        /// <summary>The most points between control points the server accepts in a fit.</summary>
        public const uint MaxInterpolations = 64;

        /// <summary>
        /// A centripetal Catmull-Rom fit with <paramref name="interpolations"/> points between control points, at most
        /// <see cref="MaxInterpolations"/> (the server refuses more).
        /// </summary>
        public static ProtoCurveFit CatmullRom(uint interpolations) =>
            new() { Method = ProtoCurveMethod.CatmullRom, Interpolations = Math.Min(interpolations, MaxInterpolations) };

        /// <summary>
        /// A geometry seed as a foreground shape: a circle as itself, a polygon with its holes, a rectangle as a polygon.
        /// Null for an edited bitmap, which goes as foreground mask tiles, and for a degenerate polygon.
        /// </summary>
        public static ProtoShape? FromSeed(SegmentationSeed seed, int downsample)
        {
            if (seed.Mask is not null)
                return null;

            if (seed.Circle is Circle circle)
                return circle.Radius > 0 ? Circle(circle, downsample) : null;

            if (seed.Rings is not null)
                return Polygon(seed.Rings, downsample);

            return Rectangle(seed.Bounds, downsample);
        }

        /// <summary>The ring without a repeated closing vertex, or null with fewer than three vertices left.</summary>
        private static ProtoRing? Ring(Vector2[] points, int downsample)
        {
            if (points is null)
                return null;

            int count = points.Length;
            if (count > 1 && points[0] == points[count - 1])
                count--;

            if (count < 3)
                return null;

            ProtoRing ring = new();
            for (int i = 0; i < count; i++)
                ring.Points.Add(ToGrid(points[i], downsample));

            return ring;
        }
    }

    /// <summary>
    /// The neighbouring annotations of a request, in the tiles' space: what the object is not. Sent as background shapes
    /// (circles as circles, polygons as polygons, lines as lines), those reaching the request's tiles with the start and the
    /// rest as the server asks for the cells they reach (<c>TilesNeeded.background_cells</c>).
    /// </summary>
    /// <remarks>Built once, then only read; safe to share between requests.</remarks>
    internal sealed class SegmentationBackgroundShapes
    {
        private enum Kind
        {
            Circle,
            Polygon,
            Line,
        }

        private sealed class Entry(Kind kind, Rectangle bounds)
        {
            public Kind Kind { get; } = kind;

            /// <summary>The shape's extent in the tiles' space, a line's half width included.</summary>
            public Rectangle Bounds { get; } = bounds;

            public Circle Circle { get; init; }

            public IReadOnlyList<Vector2[]> Rings { get; init; } = [];

            public IReadOnlyList<Vector2> Points { get; init; } = [];

            public bool Closed { get; init; }

            public double Width { get; init; }

            /// <summary>Points between control points of a fitted curve; null for straight segments.</summary>
            public uint? Interpolations { get; init; }
        }

        private readonly List<Entry> entries = [];

        /// <summary>The most shapes the server accepts in one Feature or one answer.</summary>
        public const int MaxShapesPerMessage = 512;

        /// <summary>Number of shapes.</summary>
        public int Count => entries.Count;

        /// <summary>True when there are none.</summary>
        public bool IsEmpty => entries.Count == 0;

        /// <summary>Adds a circle, its true radius in the tiles' space.</summary>
        public void AddCircle(Circle circle)
        {
            if (!(circle.Radius > 0))
                return;

            entries.Add(new Entry(Kind.Circle, circle.BoundingBox) { Circle = circle });
        }

        /// <summary>
        /// Adds a polygon given as its exterior ring followed by any holes. <paramref name="curveInterpolations"/> marks a curve
        /// polygon whose rings are control points.
        /// </summary>
        public void AddPolygon(IReadOnlyList<Vector2[]> rings, uint? curveInterpolations = null)
        {
            if (rings is null || rings.Count == 0 || rings[0] is null || rings[0].Length < 3)
                return;

            entries.Add(new Entry(Kind.Polygon, Geometry.Rectangle.GetBoundingBox(rings[0]))
            {
                Rings = rings,
                Interpolations = curveInterpolations
            });
        }

        /// <summary>
        /// Adds a line <paramref name="width"/> wide. <paramref name="curveInterpolations"/> marks a curve whose points are
        /// control points.
        /// </summary>
        public void AddLine(IReadOnlyList<Vector2> points, bool closed, double width, uint? curveInterpolations = null)
        {
            if (points is null || points.Count < 2)
                return;

            Rectangle bounds = Geometry.Rectangle.GetBoundingBox(points.ToArray());
            double half = Math.Max(0, width) / 2;
            entries.Add(new Entry(Kind.Line, new Rectangle(
                new Vector2(bounds.Left - half, bounds.Bottom - half),
                new Vector2(bounds.Right + half, bounds.Top + half)))
            {
                Points = points,
                Closed = closed,
                Width = width,
                Interpolations = curveInterpolations
            });
        }

        /// <summary>
        /// The indices of the shapes whose extent reaches any of <paramref name="cells"/> on the grid at
        /// <paramref name="downsample"/>, skipping (and adding to) <paramref name="sent"/>, in the order they were added, at most
        /// <see cref="MaxShapesPerMessage"/>: a shape left over stays unsent and can go with a later answer for a cell it reaches.
        /// </summary>
        public List<int> TakeReaching(IEnumerable<TileCell> cells, int downsample, ISet<int> sent)
        {
            List<int> reaching = [];
            if (IsEmpty || cells is null)
                return reaching;

            double cellWorld = SegmentationTileGrid.TileSize * (double)downsample;
            List<Rectangle> cellBounds = [.. cells.Select(cell => new Rectangle(
                new Vector2(cell.Col * cellWorld, cell.Row * cellWorld),
                new Vector2((cell.Col + 1) * cellWorld, (cell.Row + 1) * cellWorld)))];
            for (int i = 0; i < entries.Count && reaching.Count < MaxShapesPerMessage; i++)
            {
                if (sent.Contains(i))
                    continue;

                Rectangle bounds = entries[i].Bounds;
                if (cellBounds.Any(cell => Reaches(bounds, cell)) && sent.Add(i))
                    reaching.Add(i);
            }

            return reaching;
        }

        /// <summary>Shape <paramref name="index"/> in the grid frame at <paramref name="downsample"/>; null when it is degenerate there.</summary>
        public ProtoShape? ToShape(int index, int downsample)
        {
            Entry entry = entries[index];
            ProtoCurveFit? fit = entry.Interpolations is uint interpolations ? SegmentationPromptShapes.CatmullRom(interpolations) : null;
            return entry.Kind switch
            {
                Kind.Circle => SegmentationPromptShapes.Circle(entry.Circle, downsample),
                Kind.Polygon => SegmentationPromptShapes.Polygon(entry.Rings, downsample, fit),
                _ => SegmentationPromptShapes.Line(entry.Points, entry.Closed, entry.Width, downsample, fit),
            };
        }

        /// <summary>The shapes at <paramref name="indices"/> in the grid frame, degenerate ones left out.</summary>
        public IEnumerable<ProtoShape> ToShapes(IEnumerable<int> indices, int downsample)
        {
            foreach (int index in indices)
            {
                if (ToShape(index, downsample) is ProtoShape shape)
                    yield return shape;
            }
        }

        /// <summary>Closed-open overlap: a shape that only touches a cell's far edge does not reach it.</summary>
        private static bool Reaches(Rectangle shape, Rectangle cell) =>
            shape.Left < cell.Right && shape.Right >= cell.Left &&
            shape.Bottom < cell.Top && shape.Top >= cell.Bottom;
    }
}
