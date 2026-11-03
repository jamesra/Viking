using System;

namespace Viking.Common
{
    /// <summary>
    /// Splits a rectangular frame export into a grid of render-target-sized tiles.
    /// Used by <c>SectionViewerControl.ExportImage</c>; kept free of UI and graphics types so it can be tested.
    /// All world values are volume-space pixels; captured values are output pixels after dividing by downsample.
    /// </summary>
    public readonly struct FrameExportPlan
    {
        /// <summary>
        /// Largest captured width or height rendered in a single pass; larger frames are split into a grid.
        /// </summary>
        public const int SingleTileMaxPixels = 2048;

        /// <summary>Number of tile columns in the grid; 1 when the frame is rendered in a single pass.</summary>
        public int NumTilesX { get; }

        /// <summary>Number of tile rows in the grid; 1 when the frame is rendered in a single pass.</summary>
        public int NumTilesY { get; }

        /// <summary>World-space width of one tile (the requested width divided by <see cref="NumTilesX"/>, truncated).</summary>
        public int WorldTileSizeX { get; }

        /// <summary>World-space height of one tile (the requested height divided by <see cref="NumTilesY"/>, truncated).</summary>
        public int WorldTileSizeY { get; }

        /// <summary>Output pixel width of one tile.</summary>
        public int CapturedTileSizeX { get; }

        /// <summary>Output pixel height of one tile.</summary>
        public int CapturedTileSizeY { get; }

        private readonly int centerX;
        private readonly int centerY;

        /// <summary>
        /// Plans the export of a frame of <paramref name="worldWidth"/> by <paramref name="worldHeight"/> centered on
        /// (<paramref name="centerX"/>, <paramref name="centerY"/>).
        /// The frame is rendered in one pass when both captured dimensions fit in <see cref="SingleTileMaxPixels"/>;
        /// otherwise it is cut into the fewest equal tiles whose captured size is at most <paramref name="maxTilePixels"/> on each axis.
        /// </summary>
        /// <param name="centerX">Frame center X in volume space.</param>
        /// <param name="centerY">Frame center Y in volume space.</param>
        /// <param name="worldWidth">Frame width in volume space.</param>
        /// <param name="worldHeight">Frame height in volume space.</param>
        /// <param name="downsample">Volume-space pixels per output pixel; captured sizes are world sizes divided by this.</param>
        /// <param name="maxTilePixels">Largest captured tile width or height, in output pixels, when the frame must be split (the device's maximum render target side).</param>
        public FrameExportPlan(double centerX, double centerY, double worldWidth, double worldHeight, double downsample, int maxTilePixels)
        {
            this.centerX = (int)Math.Floor(centerX);
            this.centerY = (int)Math.Floor(centerY);

            int requestedWidth = (int)Math.Round(worldWidth);
            int requestedHeight = (int)Math.Round(worldHeight);
            int capturedWidth = (int)Math.Round(worldWidth / downsample);
            int capturedHeight = (int)Math.Round(worldHeight / downsample);

            if (capturedWidth <= SingleTileMaxPixels && capturedHeight <= SingleTileMaxPixels)
            {
                NumTilesX = 1;
                NumTilesY = 1;
                WorldTileSizeX = requestedWidth;
                WorldTileSizeY = requestedHeight;
                CapturedTileSizeX = capturedWidth;
                CapturedTileSizeY = capturedHeight;
                return;
            }

            NumTilesX = Math.Max(1, (int)Math.Ceiling((double)capturedWidth / maxTilePixels));
            NumTilesY = Math.Max(1, (int)Math.Ceiling((double)capturedHeight / maxTilePixels));
            WorldTileSizeX = requestedWidth / NumTilesX;
            WorldTileSizeY = requestedHeight / NumTilesY;
            CapturedTileSizeX = (int)Math.Round(WorldTileSizeX / downsample);
            CapturedTileSizeY = (int)Math.Round(WorldTileSizeY / downsample);
        }

        /// <summary>Total output width in pixels if the tiles were stitched.</summary>
        public int FinalImageWidth => CapturedTileSizeX * NumTilesX;

        /// <summary>Total output height in pixels if the tiles were stitched.</summary>
        public int FinalImageHeight => CapturedTileSizeY * NumTilesY;

        /// <summary>
        /// World-space center of the tile at (<paramref name="col"/>, <paramref name="row"/>). The grid is centered on the
        /// frame center, so a one-tile plan returns the frame center itself.
        /// </summary>
        public (double X, double Y) TileCenter(int col, int row) =>
            (centerX + ((col - ((NumTilesX - 1) / 2.0)) * WorldTileSizeX),
             centerY + ((row - ((NumTilesY - 1) / 2.0)) * WorldTileSizeY));
    }
}
