using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Viking.VolumeModel;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// A pen stroke that crosses a location's outline more than twice, or crosses two of its rings, such as one that cuts a
    /// bridge out of a cell: down through the bridge, along the far side and back up. The stroke, closed by a straight line
    /// between its ends, encloses a region. A stroke that starts and ends outside the location removes that region; one that
    /// starts and ends inside adds it. When the cut leaves several pieces, each is offered as a choice.
    /// The boolean is done on the drawn curves, so the result follows what is on screen. Each new outline is then fitted with
    /// control points again (<see cref="CatmullRomControlPointSimplification.RefineControlPoints"/>): the location's stored
    /// points on it are kept exactly, the points where the stroke meets the outline are added, and the stroke and the old curve
    /// supply any more the curve needs to stay within the tolerance.
    /// </summary>
    internal static class PenRegionCut
    {
        /// <summary>Pieces smaller than this (square volume pixels) are slivers of the boolean, not choices.</summary>
        private const double MinPieceArea = 4.0;

        /// <summary>Grid, in volume pixels, for recognizing an outline point the boolean passed through unchanged.</summary>
        private const double MatchGrid = 1e-3;

        private enum Origin
        {
            Junction,
            OldCurve,
            Pen,
        }

        private readonly record struct Tag(Origin Origin, int Ring, int ControlIndex);

        /// <summary>The choices for the stroke, largest piece first. Empty when the stroke does not enclose a region to cut or add.</summary>
        /// <param name="penPath">The pen stroke in volume space.</param>
        /// <param name="tolerance">Largest distance, in volume pixels, between a new curve and the stroke or the old curve.</param>
        public static List<Change2DContourAction> GetActions(LocationObj location, IVolumeToSectionTransform transform, IReadOnlyList<Vector2> penPath, double tolerance)
        {
            List<Change2DContourAction> output = [];
            if (location is null || penPath is null || penPath.Count < 3)
                return output;

            Polygon stored = location.MosaicShape.ToPolygon();
            List<Vector2[]> storedRings = [stored.ExteriorRing, .. stored.InteriorRings];
            uint interpolations = Global.AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay;

            Dictionary<(long, long), Tag> tags = [];
            Polygon drawn = null;
            for (int r = 0; r < storedRings.Count; r++)
            {
                if (transform.TrySectionToVolume(storedRings[r], out Vector2[] volumeRing).Any(mapped => !mapped))
                    return Rejected(output, location.ID, "the outline is partly outside the section mapping");

                ControlRingCurve curve;
                try
                {
                    curve = new ControlRingCurve(volumeRing, interpolations);
                }
                catch (ArgumentException e)
                {
                    return Rejected(output, location.ID, e.Message);
                }

                Vector2[] dense = curve.Curve;
                for (int i = 0; i < dense.Length - 1; i++)
                    tags[Key(dense[i])] = new Tag(Origin.OldCurve, r, curve.ControlIndexAt(i));

                try
                {
                    if (r == 0)
                        drawn = new Polygon(dense);
                    else
                        drawn.AddInteriorRing(dense);
                }
                catch (ArgumentException e)
                {
                    return Rejected(output, location.ID, $"the drawn outline is not a valid polygon: {e.Message}");
                }
            }

            foreach (Vector2 p in penPath)
            {
                if (!tags.ContainsKey(Key(p)))
                    tags[Key(p)] = new Tag(Origin.Pen, -1, -1);
            }

            bool startsInside = drawn.Covers(penPath[0]);
            if (startsInside != drawn.Covers(penPath[penPath.Count - 1]))
                return Rejected(output, location.ID, "the stroke starts and ends on different sides of the outline");

            SqlGeometry result;
            try
            {
                SqlGeometry shape = EnsureValid(drawn.ToSqlGeometry());
                SqlGeometry region = EnsureValid(ClosedStroke(penPath));
                result = EnsureValid(startsInside ? shape.STUnion(region) : shape.STDifference(region));
                if (Math.Abs(result.STArea().Value - shape.STArea().Value) < MinPieceArea)
                    return Rejected(output, location.ID, "the stroke encloses no part of the location to cut or add");
            }
            catch (Exception e) when (e is ArgumentException or FormatException or SqlTypeException)
            {
                return Rejected(output, location.ID, $"the region could not be cut: {e.Message}");
            }

            List<(Polygon Volume, Polygon Mosaic, double Area)> pieces = [];
            foreach (SqlGeometry part in AutoPolygonizeSelection.EnumeratePolygons(result))
            {
                if (part.STArea().Value < MinPieceArea)
                    continue;

                if (TryConvertPiece(part, out Polygon piece) &&
                    TryFit(piece, storedRings, tags, transform, tolerance, interpolations, out Polygon volume, out Polygon mosaic))
                    pieces.Add((volume, mosaic, part.STArea().Value));
            }

            RetraceCommandAction cut = startsInside ? RetraceCommandAction.GROW_EXTERIOR_RING : RetraceCommandAction.SHRINK_EXTERIOR_RING;
            int choice = 0;
            foreach ((Polygon volume, Polygon mosaic, double _) in pieces.OrderByDescending(p => p.Area))
                output.Add(new Change2DContourAction(location, cut, mosaic, volume, ClockwiseContour: choice++ % 2 == 1, transform));

            if (output.Count == 0)
                return Rejected(output, location.ID, "no piece of the cut could be fitted");

            Trace.WriteLine($"Pen region cut of location {location.ID}: {(startsInside ? "adds" : "removes")} the enclosed region, {output.Count} choice(s).");
            return output;
        }

        /// <summary>
        /// Converts one piece of the boolean to a <see cref="Polygon"/>, or returns false for a piece it cannot represent. SQL
        /// Server can return a ring that touches itself at a vertex (a hole pinched where the stroke meets it), which
        /// <see cref="Polygon"/> rejects as a duplicate point. Such a piece is not offered, like a piece that cannot be fitted.
        /// </summary>
        internal static bool TryConvertPiece(SqlGeometry part, out Polygon piece)
        {
            try
            {
                piece = part.ToPolygon();
                return true;
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine($"Pen region cut: a piece is not a valid polygon: {e.Message}");
                piece = null;
                return false;
            }
        }

        /// <summary>Fits one piece of the boolean, ring by ring, and builds it in volume and section space.</summary>
        private static bool TryFit(Polygon piece, List<Vector2[]> storedRings, Dictionary<(long, long), Tag> tags, IVolumeToSectionTransform transform,
                                   double tolerance, uint interpolations, out Polygon volume, out Polygon mosaic)
        {
            volume = null;
            mosaic = null;
            List<Vector2[]> volumeRings = [];
            List<Vector2[]> mosaicRings = [];
            foreach (Vector2[] ring in new[] { piece.ExteriorRing }.Concat(piece.InteriorRings))
            {
                if (!TryFitRing(ring, storedRings, tags, transform, tolerance, interpolations, out Vector2[] volumeRing, out Vector2[] mosaicRing))
                    return false;

                volumeRings.Add(volumeRing);
                mosaicRings.Add(mosaicRing);
            }

            try
            {
                volume = new Polygon(volumeRings[0]);
                mosaic = new Polygon(mosaicRings[0]);
                for (int i = 1; i < volumeRings.Count; i++)
                {
                    volume.AddInteriorRing(volumeRings[i]);
                    mosaic.AddInteriorRing(mosaicRings[i]);
                }

                return true;
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine($"Pen region cut: a piece is not a valid polygon once fitted: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Picks the control points of one ring of the result: stored points on it and the junctions to start with, then as many
        /// stroke and old curve points as the curve needs. Stored points are taken from the stored ring in section space, the rest
        /// are mapped.
        /// </summary>
        private static bool TryFitRing(Vector2[] closedRing, List<Vector2[]> storedRings, Dictionary<(long, long), Tag> tags, IVolumeToSectionTransform transform,
                                       double tolerance, uint interpolations, out Vector2[] volumeRing, out Vector2[] mosaicRing)
        {
            volumeRing = null;
            mosaicRing = null;
            Vector2[] reference = closedRing.Length > 1 && closedRing[0] == closedRing[closedRing.Length - 1]
                ? closedRing.Take(closedRing.Length - 1).ToArray()
                : closedRing;
            if (reference.Length < 3)
                return false;

            Tag[] found = [.. reference.Select(Lookup(tags))];

            // The junctions are corners where the stroke meets the old curve
            List<Vector2> points = CatmullRomControlPointSimplification.DensifyNextToCorners(reference, [.. found.Select(t => t.Origin == Origin.Junction)], tolerance, out List<int> source);
            Tag[] origin = [.. source.Select(s => s >= 0 ? found[s] : new Tag(Origin.Pen, -1, -1))];
            IEnumerable<int> start = Enumerable.Range(0, points.Count)
                .Where(i => source[i] >= 0 && (origin[i].Origin == Origin.Junction || IsStoredControl(origin[i])));
            List<int> indices = CatmullRomControlPointSimplification.RefineControlPoints(points, start, isClosed: true, tolerance, interpolations);
            if (indices.Count < 3)
                return false;

            Vector2[] controls = [.. indices.Select(i => points[i])];
            Vector2[] newPoints = [.. indices.Where(i => !IsStoredControl(origin[i])).Select(i => points[i])];
            Vector2[] mapped = [];
            if (newPoints.Length > 0 && transform.TryVolumeToSection(newPoints, out mapped).Any(ok => !ok))
                return false;

            Vector2[] section = new Vector2[indices.Count];
            int next = 0;
            for (int k = 0; k < indices.Count; k++)
            {
                Tag tag = origin[indices[k]];
                section[k] = IsStoredControl(tag) ? storedRings[tag.Ring][tag.ControlIndex] : mapped[next++];
            }

            volumeRing = [.. controls, controls[0]];
            mosaicRing = [.. section, section[0]];
            return true;
        }

        private static bool IsStoredControl(Tag tag) => tag.Origin == Origin.OldCurve && tag.ControlIndex >= 0;

        /// <summary>Where a point of the boolean's result came from; a point found nowhere is a junction the boolean made.</summary>
        private static Func<Vector2, Tag> Lookup(Dictionary<(long, long), Tag> tags) => p =>
        {
            (long x, long y) = Key(p);
            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    if (tags.TryGetValue((x + dx, y + dy), out Tag tag))
                        return tag;
                }
            }

            return new Tag(Origin.Junction, -1, -1);
        };

        private static (long, long) Key(Vector2 p) => ((long)Math.Round(p.X / MatchGrid), (long)Math.Round(p.Y / MatchGrid));

        /// <summary>The stroke closed by a straight line between its ends, made valid where it crosses itself.</summary>
        private static SqlGeometry ClosedStroke(IReadOnlyList<Vector2> penPath)
        {
            StringBuilder wkt = new("POLYGON ((");
            foreach (Vector2 p in penPath.Append(penPath[0]))
                wkt.Append(p.X.ToString("R", CultureInfo.InvariantCulture)).Append(' ').Append(p.Y.ToString("R", CultureInfo.InvariantCulture)).Append(", ");

            wkt.Length -= 2;
            wkt.Append("))");
            return SqlGeometry.STPolyFromText(new SqlChars(wkt.ToString()), 0);
        }

        private static SqlGeometry EnsureValid(SqlGeometry geometry) =>
            geometry is null || geometry.IsNull || geometry.STIsValid().IsTrue ? geometry : geometry.MakeValid();

        private static List<Change2DContourAction> Rejected(List<Change2DContourAction> output, long locationId, string reason)
        {
            Trace.WriteLine($"Pen region cut of location {locationId} offered no change: {reason}.");
            return output;
        }
    }
}
