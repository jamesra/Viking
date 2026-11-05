using System;
using System.Collections.Generic;
using System.Linq;
using Viking.gRPC.Geometry.V1;
using Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.SegmentationPrompts.Geometry;
using GeometryPoint = Viking.SegmentationPrompts.Geometry.Point;

namespace Viking.SegmentationPrompts.Rules
{
    /// <summary>Location.TypeCode values the prompt rules use.</summary>
    internal static class LocationTypeCode
    {
        public const int Circle = 1;
        public const int Polygon = 4;
        public const int CurvePolygon = 6;
        public const int ClosedCurve = 7;
    }

    /// <summary>
    /// One location's geometry as the prompt sends it: its shapes (a circle at its TRUE mosaic radius, or a polygon per
    /// WKT part with its holes, a curve polygon's rings fitted as the training exporter fits them) and the closed grid
    /// cell range its bounds reach.
    /// </summary>
    internal sealed class AnnotationGeometry
    {
        /// <summary>Side of a grid cell at downsample 1 (the server's 1024 x 1024 tiles).</summary>
        public const int CellSize = 1024;

        public PromptAnnotation Annotation { get; }
        public bool IsCircle { get; }
        public IReadOnlyList<Shape> Shapes { get; }
        public CellRange Cells { get; }

        private AnnotationGeometry(PromptAnnotation annotation, bool isCircle, IReadOnlyList<Shape> shapes, CellRange cells)
        {
            Annotation = annotation;
            IsCircle = isCircle;
            Shapes = shapes;
            Cells = cells;
        }

        /// <summary>True for the type codes a prompt can carry: circles and closed areas.</summary>
        public static bool IsPromptType(int typeCode) =>
            typeCode == LocationTypeCode.Circle || IsAreaType(typeCode);

        private static bool IsAreaType(int typeCode) =>
            typeCode == LocationTypeCode.Polygon || typeCode == LocationTypeCode.CurvePolygon ||
            typeCode == LocationTypeCode.ClosedCurve;

        /// <summary>The location's geometry.</summary>
        /// <exception cref="ArgumentException">A type code that is not a circle or an area, or a circle whose radius is
        /// not positive and finite.</exception>
        /// <exception cref="FormatException">An area whose WKT is not a POLYGON or MULTIPOLYGON.</exception>
        public static AnnotationGeometry From(PromptAnnotation annotation)
        {
            if (annotation.TypeCode == LocationTypeCode.Circle)
                return FromCircle(annotation);
            if (IsAreaType(annotation.TypeCode))
                return FromArea(annotation);
            throw new ArgumentException(
                $"Location {annotation.Id} has TypeCode {annotation.TypeCode}; a prompt takes circles (1) and areas (4, 6, 7).");
        }

        private static AnnotationGeometry FromCircle(PromptAnnotation annotation)
        {
            double r = annotation.Radius;
            if (!(r > 0) || double.IsInfinity(r) || double.IsNaN(annotation.X) || double.IsInfinity(annotation.X) ||
                double.IsNaN(annotation.Y) || double.IsInfinity(annotation.Y))
            {
                throw new ArgumentException($"Circle location {annotation.Id} has centre ({annotation.X}, {annotation.Y}) and radius {r}.");
            }
            var shape = new Shape
            {
                Circle = new Circle { Center = new Vector2 { X = annotation.X, Y = annotation.Y }, Radius = r },
            };
            var cells = CellRange.Of(annotation.X - r, annotation.Y - r, annotation.X + r, annotation.Y + r);
            return new AnnotationGeometry(annotation, true, new[] { shape }, cells);
        }

        private static AnnotationGeometry FromArea(PromptAnnotation annotation)
        {
            // Viking draws a curve polygon and a closed curve through their control points; the exporter fitted every
            // ring of a curve polygon, holes included, before it rasterized the training masks.
            bool fit = annotation.TypeCode != LocationTypeCode.Polygon;
            var shapes = new List<Shape>();
            double xMin = double.PositiveInfinity, yMin = double.PositiveInfinity;
            double xMax = double.NegativeInfinity, yMax = double.NegativeInfinity;
            foreach (List<List<GeometryPoint>> polygon in MosaicWkt.ParsePolygons(annotation.MosaicWkt))
            {
                List<List<GeometryPoint>> rings = polygon.Select(ring => Open(fit ? ClosedCurveFit.FitClosedRing(ring) : ring)).ToList();
                if (rings.Any(ring => ring.Count < 3))
                    throw new FormatException($"Location {annotation.Id}'s MosaicShape has a ring under three points.");
                foreach (GeometryPoint point in rings[0])
                {
                    xMin = Math.Min(xMin, point.X);
                    yMin = Math.Min(yMin, point.Y);
                    xMax = Math.Max(xMax, point.X);
                    yMax = Math.Max(yMax, point.Y);
                }
                var shape = new Polygon { Exterior = ToRing(rings[0]) };
                shape.Holes.AddRange(rings.Skip(1).Select(ToRing));
                shapes.Add(new Shape { Polygon = shape });
            }
            if (shapes.Count == 0)
                throw new FormatException($"Location {annotation.Id}'s MosaicShape has no polygon.");
            return new AnnotationGeometry(annotation, false, shapes, CellRange.Of(xMin, yMin, xMax, yMax));
        }

        /// <summary>The ring without its closing point: a Ring message does not repeat the first vertex.</summary>
        private static List<GeometryPoint> Open(List<GeometryPoint> ring)
        {
            if (ring.Count > 1 && ring[0].X == ring[ring.Count - 1].X && ring[0].Y == ring[ring.Count - 1].Y)
                ring.RemoveAt(ring.Count - 1);
            return ring;
        }

        private static Ring ToRing(List<GeometryPoint> points)
        {
            var ring = new Ring();
            ring.Points.AddRange(points.Select(p => new Vector2 { X = p.X, Y = p.Y }));
            return ring;
        }
    }

    /// <summary>
    /// The grid cells, inclusive, that a closed box reaches: rows <c>floor(y_min / 1024)</c> to <c>floor(y_max / 1024)</c>
    /// and the same for columns. A shape reaches a cell when its bounds meet the cell's half-open square.
    /// </summary>
    internal readonly struct CellRange
    {
        public readonly int RowMin, ColMin, RowMax, ColMax;

        public CellRange(int rowMin, int colMin, int rowMax, int colMax)
        {
            RowMin = rowMin;
            ColMin = colMin;
            RowMax = rowMax;
            ColMax = colMax;
        }

        public static CellRange Of(double xMin, double yMin, double xMax, double yMax) => new CellRange(
            (int)Math.Floor(yMin / AnnotationGeometry.CellSize), (int)Math.Floor(xMin / AnnotationGeometry.CellSize),
            (int)Math.Floor(yMax / AnnotationGeometry.CellSize), (int)Math.Floor(xMax / AnnotationGeometry.CellSize));

        public bool Contains(int row, int col) => row >= RowMin && row <= RowMax && col >= ColMin && col <= ColMax;

        public bool Meets(CellRange other) =>
            RowMin <= other.RowMax && other.RowMin <= RowMax && ColMin <= other.ColMax && other.ColMin <= ColMax;

        public CellRange Union(CellRange other) => new CellRange(
            Math.Min(RowMin, other.RowMin), Math.Min(ColMin, other.ColMin),
            Math.Max(RowMax, other.RowMax), Math.Max(ColMax, other.ColMax));

        public CellRange Grown(int cells) => new CellRange(RowMin - cells, ColMin - cells, RowMax + cells, ColMax + cells);

        /// <summary>The cells, row by row from the lowest, each row from the lowest column.</summary>
        public IEnumerable<(int Row, int Col)> RowMajor()
        {
            for (int row = RowMin; row <= RowMax; row++)
                for (int col = ColMin; col <= ColMax; col++)
                    yield return (row, col);
        }
    }
}
