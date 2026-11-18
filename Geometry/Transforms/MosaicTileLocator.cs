using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Geometry.Transforms
{
    /// <summary>How a mosaic point was placed on a tile by <see cref="MosaicTileLocator.Locate"/>.</summary>
    public enum TileMappingStatus
    {
        /// <summary>Inside the image of the nearest-centre tile among those whose outline holds the point.</summary>
        Mapped = 0,

        /// <summary>
        /// Inside a tile's outline and its transform inverts there, but the pixel is off every candidate's image (a warp
        /// edge). The nearest-centre tile that inverts is used.
        /// </summary>
        Extrapolated = 1,

        /// <summary>No tile of the mosaic covers the point; there is no tile position.</summary>
        NoTile = 2,
    }

    /// <summary>A mosaic point as a position on one tile image: the tile's number and the pixel on it.</summary>
    /// <summary>
    /// A best-guess move for a mosaic point that lies on no tile (<see cref="MosaicTileLocator.TryEstimateShift"/>): how far
    /// the new mosaic moved the old mosaic's nearest tile reference point.
    /// </summary>
    public readonly struct TileShiftEstimate
    {
        public TileShiftEstimate(int tileNumber, Vector2 reference, Vector2 shift, double distance)
        {
            TileNumber = tileNumber;
            Reference = reference;
            Shift = shift;
            Distance = distance;
        }

        /// <summary>The tile whose reference point was used.</summary>
        public int TileNumber { get; }

        /// <summary>The reference point (an image corner, edge midpoint or centre) in the OLD mosaic.</summary>
        public Vector2 Reference { get; }

        /// <summary>New mosaic position of the reference point minus its old position.</summary>
        public Vector2 Shift { get; }

        /// <summary>Distance in old mosaic pixels from the point to the reference point.</summary>
        public double Distance { get; }
    }

    public readonly struct TilePoint
    {
        public TilePoint(int tileNumber, Vector2 pixel, TileMappingStatus status)
        {
            TileNumber = tileNumber;
            Pixel = pixel;
            Status = status;
        }

        /// <summary>The tile number parsed from the tile's file name (<see cref="TileTransformInfo.TileNumber"/>); -1 for <see cref="TileMappingStatus.NoTile"/>.</summary>
        public int TileNumber { get; }

        /// <summary>Full-resolution pixel on the tile image (the transform's mapped space).</summary>
        public Vector2 Pixel { get; }

        public TileMappingStatus Status { get; }

        public static TilePoint NoTile { get; } = new(-1, new Vector2(double.NaN, double.NaN), TileMappingStatus.NoTile);
    }

    /// <summary>
    /// Maps points between a section's mosaic space and its tile images, through the tile transforms of one <c>.mosaic</c>
    /// file. A tile transform's <c>Transform</c> maps tile pixels to mosaic space and <c>InverseTransform</c> the reverse.
    /// </summary>
    /// <remarks>
    /// <para>Where tiles overlap, a point lies on several images and the assembled mosaic shows only one of them: nornir's
    /// Assemble composites with the tile whose centre is closest. <see cref="Locate"/> picks the tile by the same rule, so a
    /// point follows the tile it was drawn on (nornir <c>mosaic_remap.MosaicPointRemapper</c>, which this mirrors).</para>
    /// <para>Remapping annotations from an old mosaic to a rebuilt one is <see cref="Locate"/> on the old locator followed by
    /// <see cref="TryMapFromTile"/> on the new one with the same tile number: the tile images do not change, only where they
    /// sit.</para>
    /// </remarks>
    public sealed class MosaicTileLocator
    {
        /// <summary>Pixels of slack when testing whether an inverse-mapped point lies on a tile image.</summary>
        public const double DefaultMargin = 2.0;

        private readonly Tile[] _Tiles;
        private readonly Dictionary<int, Tile> _ByNumber;
        private readonly RTree.RTree<int> _Index = new();
        private readonly double _Margin;

        /// <summary>Builds the locator from the tile transforms <see cref="TransformFactory.LoadMosaic"/> returns.</summary>
        /// <exception cref="ArgumentException">A transform carries no <see cref="TileTransformInfo"/>, or two tiles have the same number.</exception>
        public MosaicTileLocator(IEnumerable<ITransform> tileTransforms, double margin = DefaultMargin)
        {
            if (tileTransforms is null)
                throw new ArgumentNullException(nameof(tileTransforms));

            _Margin = margin;
            List<Tile> tiles = [];
            _ByNumber = [];
            foreach (ITransform transform in tileTransforms.Where(t => t is not null))
            {
                if (transform is not ITransformInfo { Info: TileTransformInfo info })
                    throw new ArgumentException("Every tile transform must carry a TileTransformInfo", nameof(tileTransforms));

                if (_ByNumber.ContainsKey(info.TileNumber))
                    throw new ArgumentException($"Tile number {info.TileNumber} ({info.TileFileName}) appears twice in the mosaic", nameof(tileTransforms));

                Tile tile = new(transform, info);
                _ByNumber.Add(info.TileNumber, tile);
                tiles.Add(tile);
            }

            _Tiles = [.. tiles];
            for (int i = 0; i < _Tiles.Length; i++)
                _Index.Add(_Tiles[i].Outline.ToRTreeRect(0), i);
        }

        /// <summary>Reads and parses a <c>.mosaic</c> file.</summary>
        public static MosaicTileLocator Load(string mosaicPath, double margin = DefaultMargin)
        {
            string fullPath = System.IO.Path.GetFullPath(mosaicPath);
            ITransform[] transforms = TransformFactory.LoadMosaic(System.IO.Path.GetDirectoryName(fullPath) ?? string.Empty,
                                                                  File.ReadAllLines(fullPath),
                                                                  File.GetLastWriteTimeUtc(fullPath));
            return new MosaicTileLocator(transforms, margin);
        }

        /// <summary>Number of tiles in the mosaic.</summary>
        public int TileCount => _Tiles.Length;

        /// <summary>The tile numbers in the mosaic.</summary>
        public IEnumerable<int> TileNumbers => _ByNumber.Keys;

        /// <summary>True if the mosaic places the tile with this number.</summary>
        public bool ContainsTile(int tileNumber) => _ByNumber.ContainsKey(tileNumber);

        /// <summary>The tile position of a mosaic point; see <see cref="Locate(Vector2, out TilePoint?)"/>.</summary>
        public TilePoint Locate(Vector2 mosaicPoint) => Locate(mosaicPoint, out _);

        /// <summary>
        /// The tile position of a mosaic point: the nearest-centre tile whose image holds it
        /// (<see cref="TileMappingStatus.Mapped"/>), else the nearest-centre tile whose transform inverts there
        /// (<see cref="TileMappingStatus.Extrapolated"/>), else <see cref="TilePoint.NoTile"/>.
        /// </summary>
        /// <param name="alternate">Where a second tile's image also holds the point, its tile position; used to measure how
        /// far apart two overlapping tiles put the point after remapping. Null otherwise.</param>
        public TilePoint Locate(Vector2 mosaicPoint, out TilePoint? alternate)
        {
            alternate = null;
            List<int> candidates = Candidates(mosaicPoint);
            if (candidates.Count == 0)
                return TilePoint.NoTile;

            TilePoint? chosen = null;
            TilePoint? extrapolated = null;
            foreach (int i in candidates)
            {
                Tile tile = _Tiles[i];
                if (!tile.Transform.TryInverseTransform(mosaicPoint, out Vector2 pixel))
                    continue;

                if (tile.ImageHolds(pixel, _Margin))
                {
                    TilePoint found = new(tile.Number, pixel, TileMappingStatus.Mapped);
                    if (chosen is null)
                    {
                        chosen = found;
                        continue;
                    }

                    alternate = found;
                    break;
                }

                extrapolated ??= new TilePoint(tile.Number, pixel, TileMappingStatus.Extrapolated);
            }

            return chosen ?? extrapolated ?? TilePoint.NoTile;
        }

        /// <summary>Maps a pixel on the tile with this number to mosaic space through this mosaic's transform for it.</summary>
        /// <returns>False when this mosaic has no tile with that number, or its transform cannot map the pixel.</returns>
        public bool TryMapFromTile(int tileNumber, Vector2 pixel, out Vector2 mosaicPoint)
        {
            if (!_ByNumber.TryGetValue(tileNumber, out Tile tile))
            {
                mosaicPoint = default;
                return false;
            }

            return tile.Transform.TryTransform(pixel, out mosaicPoint);
        }

        /// <summary>
        /// Best guess for a point that lies on no tile, such as an annotation drawn just past the edge of the imaged area:
        /// the shift the new mosaic gives this mosaic's nearest tile reference point (the nine image corners, edge midpoints
        /// and centre of every tile that both mosaics hold). A translation only; it ignores any rotation of that tile.
        /// </summary>
        /// <returns>False when no tile of this mosaic is in <paramref name="newMosaic"/>.</returns>
        public bool TryEstimateShift(Vector2 mosaicPoint, MosaicTileLocator newMosaic, out TileShiftEstimate estimate)
        {
            estimate = default;
            double best = double.MaxValue;
            bool found = false;
            foreach (Tile tile in _Tiles)
            {
                if (!newMosaic.ContainsTile(tile.Number))
                    continue;

                foreach ((Vector2 pixel, Vector2 placed) in tile.ReferencePoints)
                {
                    double distance = Vector2.Distance(placed, mosaicPoint);
                    if (distance >= best || !newMosaic.TryMapFromTile(tile.Number, pixel, out Vector2 moved))
                        continue;

                    best = distance;
                    found = true;
                    estimate = new TileShiftEstimate(tile.Number, placed, new Vector2(moved.X - placed.X, moved.Y - placed.Y), distance);
                }
            }

            return found;
        }

        /// <summary>Tiles whose outline box holds the point, nearest centre first (ties by tile number, so the order is stable).</summary>
        private List<int> Candidates(Vector2 p)
        {
            List<int> hits = _Index.Intersects(p.ToRTreeRect(0))
                                   .Where(i => _Tiles[i].OutlineHolds(p))
                                   .ToList();
            hits.Sort((a, b) =>
            {
                int byDistance = Vector2.DistanceSquared(_Tiles[a].Center, p).CompareTo(Vector2.DistanceSquared(_Tiles[b].Center, p));
                return byDistance != 0 ? byDistance : _Tiles[a].Number.CompareTo(_Tiles[b].Number);
            });
            return hits;
        }

        /// <summary>One tile: its transform, image size, and outline and centre in mosaic space.</summary>
        private sealed class Tile
        {
            public readonly ITransform Transform;
            public readonly int Number;
            public readonly double Width;
            public readonly double Height;

            /// <summary>Bounding box of the image's corners, edge midpoints and centre mapped to mosaic space.</summary>
            public readonly Rectangle Outline;

            /// <summary>The image centre mapped to mosaic space.</summary>
            public readonly Vector2 Center;

            /// <summary>The image's corners, edge midpoints and centre that the transform maps: tile pixel and mosaic position.</summary>
            public readonly (Vector2 Pixel, Vector2 Mosaic)[] ReferencePoints;

            public Tile(ITransform transform, TileTransformInfo info)
            {
                Transform = transform;
                Number = info.TileNumber;
                Width = info.ImageWidth;
                Height = info.ImageHeight;

                Vector2[] probe =
                [
                    new(0, 0), new(Width / 2, 0), new(Width, 0),
                    new(0, Height / 2), new(Width / 2, Height / 2), new(Width, Height / 2),
                    new(0, Height), new(Width / 2, Height), new(Width, Height),
                ];

                bool[] ok = transform.TryTransform(probe, out Vector2[] mapped);
                Vector2[] placed = [.. mapped.Where((_, i) => ok[i])];
                if (placed.Length == 0)
                    throw new ArgumentException($"Tile {info.TileFileName}: its transform maps none of its image's corners");

                ReferencePoints = [.. probe.Select((pixel, i) => (pixel, mapped[i])).Where((_, i) => ok[i])];
                Outline = placed.BoundingBox();
                Center = ok[4] ? mapped[4] : Outline.Center;
            }

            public bool OutlineHolds(Vector2 p) =>
                p.X >= Outline.Left && p.X <= Outline.Right && p.Y >= Outline.Bottom && p.Y <= Outline.Top;

            public bool ImageHolds(Vector2 pixel, double margin) =>
                pixel.X >= -margin && pixel.Y >= -margin && pixel.X <= Width + margin && pixel.Y <= Height + margin;
        }
    }
}
