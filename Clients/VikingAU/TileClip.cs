using Geometry;
using Geometry.Transforms;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.VolumeModel;

namespace Viking.AU
{
    enum RepairKind
    {
        /// <summary>On no tile the new mosaic places: delete it.</summary>
        Dropped,

        /// <summary>Wholly on kept tiles, but step 1 chose a dropped tile where two overlapped: remapped whole through a kept one.</summary>
        Retiled,

        /// <summary>Partly on kept tiles: the part on them is remapped, the rest is removed.</summary>
        Clipped,

        /// <summary>Could not be handled; left unchanged.</summary>
        Failed,
    }

    sealed class RepairResult
    {
        public RepairKind Kind { get; set; }

        /// <summary>The shape in the NEW mosaic (Retiled, Clipped).</summary>
        public IShape2D NewShape { get; set; }

        /// <summary>The tile mapping of the kept shape (Retiled, Clipped).</summary>
        public FromTilesResult Mapping { get; set; }

        /// <summary>The kept part in the OLD mosaic (Clipped).</summary>
        public SqlGeometry KeptOld { get; set; }

        /// <summary>The removed part in the OLD mosaic (Clipped).</summary>
        public SqlGeometry RemovedOld { get; set; }

        /// <summary>Area (px²) or length (px) of the kept and removed parts (Clipped).</summary>
        public double KeptSize { get; set; }
        public double RemovedSize { get; set; }

        /// <summary>Old tiles the original shape used that the new mosaic does not place.</summary>
        public List<int> DroppedTiles { get; set; } = [];

        public string Failure { get; set; }
    }

    /// <summary>
    /// What the remap does with a location step 1 or step 2 could not move whole (James Anderson, 2026-11-20): one on no tile
    /// the new mosaic places, in either mosaic, is dropped; one partly on kept tiles is clipped to the part on them, in the old
    /// mosaic, against the outlines of the old tiles the new mosaic still places (<see cref="MosaicTileLocator.Footprints"/>),
    /// and that part is remapped through those tiles. A circle or point goes by its centre (it has no part to keep).
    /// Where several pieces survive, the largest is kept and the others are removed with the rest.
    /// </summary>
    static class TileClip
    {
        /// <summary>A removed part smaller than this (px² for areas, px for lines) does not count as a clip.</summary>
        private const double ClipTolerance = 1.0;

        public static RepairResult Repair(IShape2D shape, MosaicTileLocator oldMosaic, MosaicTileLocator newMosaic)
        {
            Func<int, bool> kept = newMosaic.ContainsTile;
            List<int> droppedTiles = DroppedTilesUsed(shape, oldMosaic, newMosaic);

            if (shape is Circle || shape is Vector2)
                return Remap(shape, oldMosaic, newMosaic, kept, droppedTiles, null);

            SqlGeometry original = LocationVolumeGeometry.ToSqlGeometry(shape);
            int dimension = original.STDimension().Value;
            List<Vector2[]> rings = oldMosaic.Footprints(shape.BoundingBox, kept);
            if (rings.Count == 0)
                return new RepairResult { Kind = RepairKind.Dropped, DroppedTiles = droppedTiles };

            SqlGeometry region = null;
            foreach (Vector2[] ring in rings)
            {
                SqlGeometry tile = ring.Append(ring[0]).ToArray().ToPolygon().MakeValid();
                region = region is null ? tile : region.STUnion(tile);
            }

            SqlGeometry piece = Largest(original.STIntersection(region), dimension);
            if (piece is null || Measure(piece, dimension) < ClipTolerance)
                return new RepairResult { Kind = RepairKind.Dropped, DroppedTiles = droppedTiles };

            SqlGeometry removed = original.STDifference(piece);
            if (removed.IsNull || removed.STIsEmpty().Value || Measure(removed, dimension) < ClipTolerance)
                return Remap(shape, oldMosaic, newMosaic, kept, droppedTiles, null);

            IShape2D keptShape;
            try
            {
                keptShape = ToShape(piece, shape);
            }
            catch (ArgumentException e)
            {
                return new RepairResult { Kind = RepairKind.Failed, DroppedTiles = droppedTiles, Failure = "clipped part is not a valid shape: " + e.Message };
            }

            return Remap(keptShape, oldMosaic, newMosaic, kept, droppedTiles, (piece, removed, Measure(piece, dimension), Measure(removed, dimension)));
        }

        /// <summary>
        /// Maps the shape through the kept tiles only. <paramref name="clip"/> is null for a shape kept whole: a circle or point
        /// whose centre is on no kept tile is then dropped; any other shape with a vertex on no kept tile fails (its area off
        /// the kept tiles was below <see cref="ClipTolerance"/>, so nothing was clipped, but a vertex still has no tile).
        /// </summary>
        private static RepairResult Remap(IShape2D shape, MosaicTileLocator oldMosaic, MosaicTileLocator newMosaic, Func<int, bool> kept,
                                          List<int> droppedTiles, (SqlGeometry Kept, SqlGeometry Removed, double KeptSize, double RemovedSize)? clip)
        {
            ToTilesResult tiles = TileRemap.ToTiles(shape, oldMosaic, kept);
            if (tiles.Shape is null)
            {
                if (clip is null && (shape is Circle || shape is Vector2))
                    return new RepairResult { Kind = RepairKind.Dropped, DroppedTiles = droppedTiles };

                return new RepairResult
                {
                    Kind = RepairKind.Failed,
                    DroppedTiles = droppedTiles,
                    Failure = $"{(clip is null ? "shape" : "clipped part")} has {tiles.Uncovered.Count} vertex(es) on no kept tile, e.g. "
                              + string.Join(" ", tiles.Uncovered.Take(3).Select(v => FormattableString.Invariant($"({v.X:F1}, {v.Y:F1})"))),
                };
            }

            FromTilesResult mapped = TileRemap.FromTiles(tiles.Shape, newMosaic);
            if (!mapped.Succeeded)
                return new RepairResult { Kind = RepairKind.Failed, DroppedTiles = droppedTiles, Failure = mapped.Failure };

            return new RepairResult
            {
                Kind = clip is null ? RepairKind.Retiled : RepairKind.Clipped,
                NewShape = mapped.Shape,
                Mapping = mapped,
                KeptOld = clip?.Kept,
                RemovedOld = clip?.Removed,
                KeptSize = clip?.KeptSize ?? 0,
                RemovedSize = clip?.RemovedSize ?? 0,
                DroppedTiles = droppedTiles,
            };
        }

        /// <summary>The old tiles (by step 1's nearest-centre rule) the shape's vertices lie on that the new mosaic dropped.</summary>
        private static List<int> DroppedTilesUsed(IShape2D shape, MosaicTileLocator oldMosaic, MosaicTileLocator newMosaic) =>
            [.. TileRemap.Vertices(shape)
                         .Select(v => oldMosaic.Locate(v))
                         .Where(t => t.Status != TileMappingStatus.NoTile && !newMosaic.ContainsTile(t.TileNumber))
                         .Select(t => t.TileNumber)
                         .Distinct()
                         .OrderBy(t => t)];

        private static double Measure(SqlGeometry g, int dimension) =>
            dimension == 2 ? g.STArea().Value : g.STLength().Value;

        /// <summary>The largest part of <paramref name="g"/> of the given dimension (area or length); null when there is none.</summary>
        private static SqlGeometry Largest(SqlGeometry g, int dimension)
        {
            if (g is null || g.IsNull || g.STIsEmpty().Value)
                return null;

            SqlGeometry best = null;
            double bestSize = 0;
            for (int i = 1; i <= g.STNumGeometries().Value; i++)
            {
                SqlGeometry part = g.STGeometryN(i);
                if (part.STDimension().Value != dimension)
                    continue;

                double size = Measure(part, dimension);
                if (best is null || size > bestSize)
                {
                    best = part;
                    bestSize = size;
                }
            }

            return best;
        }

        /// <summary>The clipped piece as a shape of the original's kind (a two-point piece of a line segment stays a segment).</summary>
        private static IShape2D ToShape(SqlGeometry piece, IShape2D original)
        {
            if (original is Polygon)
                return piece.ToPolygon();

            Vector2[] points = [.. Enumerable.Range(1, piece.STNumPoints().Value)
                                             .Select(i => piece.STPointN(i))
                                             .Select(p => new Vector2(p.STX.Value, p.STY.Value))];
            return original is LineSegment && points.Length == 2
                ? new LineSegment(points[0], points[1])
                : new Polyline(points);
        }
    }
}
