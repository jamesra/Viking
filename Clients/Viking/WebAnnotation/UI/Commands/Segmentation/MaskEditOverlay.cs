#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;
using GeometryVector2 = Geometry.Vector2;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>The colours of a <see cref="MaskEditOverlay"/>, straight (not premultiplied) alpha.</summary>
    /// <param name="Fill">Inside pixels of the mask.</param>
    /// <param name="Edge">Inside pixels on the mask's edge (<see cref="SegmentationMaskEdit.IsEdge"/>), drawn as its outline.</param>
    /// <param name="OriginalOutline">The dashed outline of the polygon the mask was made from.</param>
    internal readonly record struct MaskEditPalette(XnaColor Fill, XnaColor Edge, XnaColor OriginalOutline);

    /// <summary>
    /// Draws a raster mask in volume space as a translucent fill with an opaque one-pixel edge, plus the outline of the polygon it
    /// was made from as a thin dashed line that keeps its screen width at any zoom. Shared by <see cref="SegmentationMaskEditCommand"/>
    /// (while the user edits) and <see cref="ResegmentPendingView"/> (dimmed, while the server works on the accepted mask).
    /// </summary>
    /// <remarks>
    /// The original rings are volume-space geometry handed in by the caller (Resegment passes the forward owner's control polygon,
    /// the rings it rasterized); this class never maps anything. They are drawn with the display interpolation of a saved closed
    /// curve, as the proposal rings are. The mask texture is made on the first draw and refilled after <see cref="SetPixels"/>;
    /// <see cref="Dispose"/> frees it. Render (UI) thread only.
    /// </remarks>
    internal sealed class MaskEditOverlay : IDisposable
    {
        /// <summary>Width of the dashed original outline, in screen pixels.</summary>
        internal const double OutlineWidthPixels = 2;

        private readonly int width;
        private readonly int height;
        private readonly MaskEditPalette palette;
        private readonly XnaColor[] colors;
        private readonly CurveView[] outlineViews;
        private byte[] pixels;
        private Geometry.Rectangle worldBounds;
        private Texture2D? texture;
        private TextureOverlayView? textureView;
        private bool dirty = true;

        /// <param name="width">Raster width in pixels.</param>
        /// <param name="height">Raster height in pixels.</param>
        /// <param name="pixels">The mask, row major, <paramref name="width"/> x <paramref name="height"/>. Kept, not copied: call <see cref="SetPixels"/> after changing it.</param>
        /// <param name="worldBounds">The rectangle the raster covers, volume space.</param>
        /// <param name="originalRings">The polygon the mask came from (exterior ring, then holes), volume space; null or empty draws none.</param>
        /// <param name="palette">The colours.</param>
        public MaskEditOverlay(int width, int height, byte[] pixels, Geometry.Rectangle worldBounds, IReadOnlyList<GeometryVector2[]>? originalRings, MaskEditPalette palette)
        {
            if (pixels is null || pixels.Length != (long)width * height)
                throw new ArgumentException($"A mask is {width} x {height} pixels.", nameof(pixels));

            this.width = width;
            this.height = height;
            this.pixels = pixels;
            this.worldBounds = worldBounds;
            this.palette = palette;
            colors = new XnaColor[pixels.Length];
            outlineViews = [.. (originalRings ?? []).Where(ring => ring is { Length: >= 3 }).Select(ring => new CurveView(
                ring,
                palette.OriginalOutline,
                TryToClose: true,
                numInterpolations: Global.NumClosedCurveInterpolationPointsForDisplay,
                lineWidth: OutlineWidthPixels,
                lineStyle: LineStyle.Dashed,
                ShowControlPoints: false))];
            if (originalRings is { Count: > 0 } && originalRings[0] is { Length: >= 3 } exterior)
                OriginalBounds = RasterMask.BoundsOf(exterior);
        }

        /// <summary>Volume-space bounds of the original rings, or null when there are none.</summary>
        public Geometry.Rectangle? OriginalBounds { get; private set; }

        /// <summary>Marks the mask for upload on the next draw; <paramref name="newPixels"/> replaces the kept array.</summary>
        public void SetPixels(byte[] newPixels)
        {
            if (newPixels is null || newPixels.Length != colors.Length)
                throw new ArgumentException("The mask keeps its size.", nameof(newPixels));

            pixels = newPixels;
            dirty = true;
        }

        /// <summary>Moves the raster: the rectangle it covers, volume space.</summary>
        public void SetWorldBounds(Geometry.Rectangle bounds)
        {
            worldBounds = bounds;
            if (textureView is not null)
                textureView.BoundingRect = bounds;
        }

        /// <summary>
        /// Draws the mask, then the dashed original outline over it. <paramref name="downsample"/> is volume units per screen pixel,
        /// which keeps the outline <see cref="OutlineWidthPixels"/> wide on screen.
        /// </summary>
        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, double downsample)
        {
            if (dirty || texture is null || texture.IsDisposed)
                Upload(graphicsDevice);

            textureView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);

            if (outlineViews.Length == 0)
                return;

            double lineWidth = Math.Max(1e-6, OutlineWidthPixels * downsample);
            DepthStencilState previousDepth = graphicsDevice.DepthStencilState;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            foreach (CurveView view in outlineViews)
            {
                view.LineWidth = lineWidth;
                view.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

            graphicsDevice.DepthStencilState = previousDepth;
        }

        /// <summary>Copies the mask into the texture as fill, edge and transparent texels, creating the texture on first use.</summary>
        private void Upload(GraphicsDevice device)
        {
            if (texture is null || texture.IsDisposed)
            {
                texture?.Dispose();
                texture = new Texture2D(device, width, height);
                textureView = new TextureOverlayView(texture, worldBounds, XnaColor.White);
            }

            SegmentationMaskEdit.Colorize(pixels, width, height, palette.Fill, palette.Edge, XnaColor.Transparent, colors);
            texture.SetData(colors);
            dirty = false;
        }

        /// <summary>Frees the texture. The overlay draws again (with a new texture) if <see cref="Draw"/> is called after.</summary>
        public void Dispose()
        {
            textureView = null;
            texture?.Dispose();
            texture = null;
            dirty = true;
        }
    }
}
