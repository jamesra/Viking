using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Geometry;
using Viking.Common;
using Viking.UI;
using Viking.ViewModels;
using Viking.VolumeModel;
using DrawingRectangle = System.Drawing.Rectangle;
using IoPath = System.IO.Path;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Downloads tiles over HTTP (or from the disk texture cache) and stitches a CPU
    /// EM crop for a Review card. Prefers the active volume transform when the entry is in
    /// volume space; falls back to the section mosaic pyramid for speed/availability.
    /// Does not touch the MonoGame graphics device.
    /// </summary>
    public static class ReviewChangeMosaicCropLoader
    {
        /// <summary>
        /// Builds a square EM crop around <paramref name="entry"/> bounds in the entry's
        /// <see cref="ReviewChangeEntry.CoordinateSpace"/>. Returns null when unavailable.
        /// </summary>
        public static async Task<Bitmap> TryLoadEmCropAsync(
            ReviewChangeEntry entry,
            int size,
            CancellationToken cancellationToken)
        {
            if (entry is null || entry.IsDeleted || size < 1)
                return null;
            if (entry.BBoxWidth <= 0 || entry.BBoxHeight <= 0)
                return null;

            VolumeViewModel volume = State.volume;
            if (volume is null)
                return null;
            if (!volume.SectionViewModels.TryGetValue(entry.Section, out SectionViewModel sectionVm))
                return null;

            string channel = State.ViewerControl?.CurrentChannel ?? sectionVm.DefaultChannel;
            string sectionTransform = sectionVm.DefaultPyramidTransform
                ?? State.ViewerControl?.CurrentTransform;

            ReviewChangeCardCrop.GetPaddedBounds(entry, out double left, out double bottom, out double spanX, out double spanY);
            double right = left + spanX;
            double top = bottom + spanY;
            var bounds = new Geometry.Rectangle(left, right, bottom, top);

            SortedDictionary<TileUniqueKey, TileViewModel> tiles = await LoadTilesAsync(
                volume,
                entry,
                channel,
                sectionTransform,
                bounds,
                size,
                cancellationToken).ConfigureAwait(false);
            if (tiles is null || tiles.Count == 0)
                return null;

            var result = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                using (var g = Graphics.FromImage(result))
                {
                    g.Clear(Color.FromArgb(255, 32, 32, 36));
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.PixelOffsetMode = PixelOffsetMode.Half;

                    foreach (TileViewModel tile in tiles.Values)
                    {
                        if (tile is null)
                            continue;
                        if (cancellationToken.IsCancellationRequested)
                        {
                            result.Dispose();
                            return null;
                        }

                        using Bitmap tileBmp = await LoadTileBitmapAsync(
                            tile,
                            sectionVm,
                            cancellationToken).ConfigureAwait(false);
                        if (tileBmp is null)
                            continue;

                        // Volume tiles are often warped — map texture UV corners into the card
                        // instead of stretching the AABB (that skews EM vs the volume mask).
                        if (!TryDrawTileByUv(g, tileBmp, tile, left, bottom, spanX, spanY, size))
                        {
                            DrawingRectangle dest = WorldRectToImageRect(tile.Bounds, left, bottom, spanX, spanY, size);
                            if (dest.Width < 1 || dest.Height < 1)
                                continue;
                            g.DrawImage(tileBmp, dest);
                        }
                    }
                }

                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Draws a tile using the world positions of UV (0,0), (1,0), and (0,1).
        /// GDI's three-point <see cref="Graphics.DrawImage(Image, PointF[])"/> maps those to
        /// bitmap upper-left, upper-right, and lower-left — matching Viking tile UV layout.
        /// </summary>
        static bool TryDrawTileByUv(
            Graphics g,
            Bitmap tileBmp,
            TileViewModel tile,
            double cropLeft,
            double cropBottom,
            double spanX,
            double spanY,
            int size)
        {
            if (tile?.Verticies is null || tile.Verticies.Length < 3)
                return false;

            if (!TryWorldAtUv(tile.Verticies, 0f, 0f, out Vector2 ul)
                || !TryWorldAtUv(tile.Verticies, 1f, 0f, out Vector2 ur)
                || !TryWorldAtUv(tile.Verticies, 0f, 1f, out Vector2 ll))
            {
                return false;
            }

            ReviewChangeCardCrop.WorldToImage(ul.X, ul.Y, cropLeft, cropBottom, spanX, spanY, size, out float x0, out float y0);
            ReviewChangeCardCrop.WorldToImage(ur.X, ur.Y, cropLeft, cropBottom, spanX, spanY, size, out float x1, out float y1);
            ReviewChangeCardCrop.WorldToImage(ll.X, ll.Y, cropLeft, cropBottom, spanX, spanY, size, out float x2, out float y2);

            // Degenerate parallelogram — fall back to AABB draw.
            if (Math.Abs((x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)) < 1e-3f)
                return false;

            // UV draw that lands entirely off the card looks like "no texture"; use AABB instead.
            float minX = Math.Min(x0, Math.Min(x1, x2));
            float maxX = Math.Max(x0, Math.Max(x1, x2));
            float minY = Math.Min(y0, Math.Min(y1, y2));
            float maxY = Math.Max(y0, Math.Max(y1, y2));
            const float margin = 8f;
            if (maxX < -margin || maxY < -margin || minX > size + margin || minY > size + margin)
                return false;

            g.DrawImage(tileBmp, new[]
            {
                new PointF(x0, y0),
                new PointF(x1, y1),
                new PointF(x2, y2)
            });
            return true;
        }

        static bool TryWorldAtUv(
            PositionNormalTextureVertex[] verts,
            float u,
            float v,
            out Vector2 world)
        {
            world = default;
            const float eps = 0.02f;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector2 t = verts[i].Texture;
                if (Math.Abs(t.X - u) <= eps && Math.Abs(t.Y - v) <= eps)
                {
                    world = verts[i].Position.XY();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Prefers volume tiles for volume-space entries, then mosaic when volume has no tiles
        /// (common after a filter re-seed for sections not yet warmed). Mosaic-space entries
        /// use the section mosaic pyramid only.
        /// </summary>
        static async Task<SortedDictionary<TileUniqueKey, TileViewModel>> LoadTilesAsync(
            VolumeViewModel volume,
            ReviewChangeEntry entry,
            string channel,
            string sectionTransform,
            Geometry.Rectangle bounds,
            int targetPixels,
            CancellationToken cancellationToken)
        {
            if (entry.CoordinateSpace == ReviewChangeCoordinateSpace.Volume)
            {
                SortedDictionary<TileUniqueKey, TileViewModel> volumeTiles = await TryVisibleTilesAsync(
                    volume.GetTileMapping(entry.Section, channel, sectionTransform),
                    entry.Section,
                    "volume",
                    bounds,
                    targetPixels,
                    cancellationToken).ConfigureAwait(false);
                if (volumeTiles != null && volumeTiles.Count > 0)
                    return volumeTiles;

                Trace.WriteLine(
                    $"Review feed: no volume tiles for loc {entry.LocationId} Z{entry.Section}; trying mosaic.",
                    "WebAnnotation");
            }

            return await TryVisibleTilesAsync(
                volume.GetTileMapping(
                    VolumeTransformName: "None",
                    entry.Section,
                    channel,
                    sectionTransform),
                entry.Section,
                "mosaic",
                bounds,
                targetPixels,
                cancellationToken).ConfigureAwait(false);
        }

        static async Task<SortedDictionary<TileUniqueKey, TileViewModel>> TryVisibleTilesAsync(
            MappingBase mapping,
            int section,
            string label,
            Geometry.Rectangle bounds,
            int targetPixels,
            CancellationToken cancellationToken)
        {
            if (!await TryInitializeAsync(mapping, section, label, cancellationToken).ConfigureAwait(false))
                return null;
            if (cancellationToken.IsCancellationRequested)
                return null;

            int level = ChooseDownsampleLevel(
                mapping.AvailableLevels,
                Math.Max(bounds.Width, bounds.Height),
                targetPixels);

            try
            {
                TilePyramid pyramid = await mapping.VisibleTilesAsync(bounds, level).ConfigureAwait(false);
                return pyramid?.GetTilesForLevel(level);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Review feed VisibleTiles ({label}) Z{section}: {ex.Message}", "WebAnnotation");
                return null;
            }
        }

        static async Task<bool> TryInitializeAsync(
            MappingBase mapping,
            int section,
            string label,
            CancellationToken cancellationToken)
        {
            if (mapping is null)
                return false;

            try
            {
                if (!mapping.Initialized)
                    await mapping.Initialize(cancellationToken).ConfigureAwait(false);
                return mapping.AvailableLevels != null && mapping.AvailableLevels.Length > 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    $"Review feed {label} mapping init section {section}: {ex.Message}",
                    "WebAnnotation");
                return false;
            }
        }

        /// <summary>
        /// Picks a pyramid level so the crop is roughly <paramref name="targetPixels"/> across.
        /// Prefers the coarsest level that is still at least as fine as the ideal downsample.
        /// </summary>
        internal static int ChooseDownsampleLevel(IReadOnlyList<int> levels, double worldSpan, int targetPixels)
        {
            if (levels is null || levels.Count == 0)
                throw new ArgumentException("No pyramid levels.", nameof(levels));

            double ideal = worldSpan / Math.Max(targetPixels, 1);
            int[] ordered = levels.OrderBy(l => l).ToArray();
            foreach (int level in ordered)
            {
                if (level >= ideal)
                    return level;
            }

            return ordered[ordered.Length - 1];
        }

        static DrawingRectangle WorldRectToImageRect(
            Geometry.Rectangle world,
            double cropLeft,
            double cropBottom,
            double spanX,
            double spanY,
            int size)
        {
            ReviewChangeCardCrop.WorldToImage(
                world.Left, world.Top, cropLeft, cropBottom, spanX, spanY, size, out float x0, out float yTop);
            ReviewChangeCardCrop.WorldToImage(
                world.Right, world.Bottom, cropLeft, cropBottom, spanX, spanY, size, out float x1, out float yBottom);

            float left = Math.Min(x0, x1);
            float top = Math.Min(yTop, yBottom);
            float width = Math.Abs(x1 - x0);
            float height = Math.Abs(yBottom - yTop);
            return new DrawingRectangle(
                (int)Math.Floor(left),
                (int)Math.Floor(top),
                Math.Max(1, (int)Math.Ceiling(width)),
                Math.Max(1, (int)Math.Ceiling(height)));
        }

        static async Task<Bitmap> LoadTileBitmapAsync(
            TileViewModel tile,
            SectionViewModel sectionVm,
            CancellationToken cancellationToken)
        {
            string cachePath = IoPath.Combine(
                State.TextureCachePath ?? "",
                sectionVm.SubPath ?? "",
                tile.TextureCacheFilePath ?? "");

            try
            {
                if (!string.IsNullOrWhiteSpace(cachePath) && File.Exists(cachePath))
                {
                    // Copy into memory so the file is not locked for the texture pipeline.
                    byte[] bytes = await Task.Run(() => File.ReadAllBytes(cachePath), cancellationToken)
                        .ConfigureAwait(false);
                    return BitmapFromBytes(bytes);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Review feed tile cache {cachePath}: {ex.Message}", "WebAnnotation");
            }

            string url = ResolveTileUrl(tile, sectionVm);
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(url))
                    {
                        byte[] bytes = await Task.Run(() => File.ReadAllBytes(url), cancellationToken)
                            .ConfigureAwait(false);
                        return BitmapFromBytes(bytes);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Review feed local tile {url}: {ex.Message}", "WebAnnotation");
                }

                return null;
            }

            try
            {
                HttpClient client = SharedResources.HttpClient;
                using HttpResponseMessage response = await client
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, 81920, cancellationToken).ConfigureAwait(false);
                return BitmapFromBytes(ms.ToArray());
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Review feed tile download {url}: {ex.Message}", "WebAnnotation");
                return null;
            }
        }

        /// <summary>
        /// Decodes image bytes into a standalone GDI bitmap. A <see cref="Bitmap"/> built on a
        /// <see cref="MemoryStream"/> keeps that stream open; cloning after load lets callers dispose
        /// the stream without blanking tile draws (which produced mask-only Review cards).
        /// </summary>
        static Bitmap BitmapFromBytes(byte[] bytes)
        {
            if (bytes is null || bytes.Length == 0)
                return null;

            using var ms = new MemoryStream(bytes);
            using var loaded = new Bitmap(ms);
            return new Bitmap(loaded);
        }

        static string ResolveTileUrl(TileViewModel tile, SectionViewModel sectionVm)
        {
            string path = tile.TextureFullPath ?? "";
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return VolumePath.Combine(sectionVm.Path, path);
        }
    }
}
