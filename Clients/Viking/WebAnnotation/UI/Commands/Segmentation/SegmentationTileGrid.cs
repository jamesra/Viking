using Geometry;
using System;
using System.Collections.Generic;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// One cell of the segmentation lattice. Row increases with world Y.
    /// </summary>
    public readonly struct TileCell : IEquatable<TileCell>
    {
        public TileCell(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public int Row { get; }
        public int Col { get; }

        public bool Equals(TileCell other) => Row == other.Row && Col == other.Col;

        public override bool Equals(object obj) => obj is TileCell other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Row * 397) ^ Col;
            }
        }
    }

    /// <summary>
    /// 1024x1024 partition anchored at the volume origin, in the view's pyramid downsample.
    /// Mosaic pixels are world divided by downsample, Y up. The segmentation server flips Y
    /// inside each tile image, which is stored top-left.
    /// </summary>
    public static class SegmentationTileGrid
    {
        public const int TileSize = 1024;

        /// <summary>
        /// Cell containing a world point. The positive boundary belongs to the next cell.
        /// </summary>
        public static TileCell CellIndex(double worldX, double worldY, int downsample)
        {
            if (downsample <= 0)
                throw new ArgumentOutOfRangeException(nameof(downsample));

            double cellWorld = TileSize * (double)downsample;
            return new TileCell(
                (int)Math.Floor(worldY / cellWorld),
                (int)Math.Floor(worldX / cellWorld));
        }

        /// <summary>
        /// Full cells that intersect the world rectangle. A max edge that lands on a boundary
        /// does not include the next cell.
        /// </summary>
        public static List<TileCell> CellsCovering(double minX, double minY, double maxX, double maxY, int downsample)
        {
            if (downsample <= 0)
                throw new ArgumentOutOfRangeException(nameof(downsample));
            if (maxX < minX || maxY < minY)
                return [];

            double cellWorld = TileSize * (double)downsample;
            double epsilon = cellWorld * 1e-9;
            double maxXInside = maxX <= minX ? minX : Math.Max(minX, maxX - epsilon);
            double maxYInside = maxY <= minY ? minY : Math.Max(minY, maxY - epsilon);
            TileCell low = CellIndex(minX, minY, downsample);
            TileCell high = CellIndex(maxXInside, maxYInside, downsample);
            List<TileCell> cells = new();
            for (int row = low.Row; row <= high.Row; row++)
            {
                for (int col = low.Col; col <= high.Col; col++)
                    cells.Add(new TileCell(row, col));
            }
            return cells;
        }

        /// <summary>
        /// World-pixel coordinate of a point, Y up, for the server's mosaic space.
        /// </summary>
        public static (int X, int Y) WorldToMosaicPixel(double worldX, double worldY, int downsample)
        {
            if (downsample <= 0)
                throw new ArgumentOutOfRangeException(nameof(downsample));
            return ((int)Math.Floor(worldX / downsample), (int)Math.Floor(worldY / downsample));
        }

        /// <summary>
        /// Map a fused-mosaic pixel (Y down from the top of the image) back to world.
        /// originX/originY are the mosaic's bottom-left in downsample pixels.
        /// </summary>
        public static Vector2 MosaicPixelToWorld(
            int originX,
            int originY,
            int pixelX,
            int pixelYFromTop,
            int mosaicHeight,
            int downsample)
        {
            if (downsample <= 0)
                throw new ArgumentOutOfRangeException(nameof(downsample));
            int flippedY = mosaicHeight - pixelYFromTop;
            return new Vector2(
                (originX + pixelX) * (double)downsample,
                (originY + flippedY) * (double)downsample);
        }
    }
}
