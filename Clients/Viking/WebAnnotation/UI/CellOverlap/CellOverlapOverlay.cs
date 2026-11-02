using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Viking.UI.Controls;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotation.View;
using WebAnnotationModel;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;
using XnaVector4 = Microsoft.Xna.Framework.Vector4;

namespace WebAnnotation.UI.CellOverlap
{
    /// <summary>
    /// Shades the areas where two cells' outlines overlap with a checkerboard of both cells' colors, and offers a menu there
    /// that gives the area to one of the two cells (<see cref="AssignOverlapAction"/>). Cell outlines should never overlap
    /// (<c>cell-boundaries-never-overlap.mdc</c>); this shows, and lets the user repair, the ones that do.
    /// </summary>
    /// <remarks>
    /// <see cref="Update"/> and <see cref="Draw"/> run every frame on the UI thread. Update is cheap while no outline changed
    /// (<see cref="CellOverlapIndex"/>); a changed index drops the clip textures, which are drawn again the next time a
    /// region is on screen. The squares are a fixed number of screen pixels wide at any zoom.
    /// </remarks>
    internal sealed class CellOverlapOverlay : IDisposable
    {
        /// <summary>Width of a checker square on screen, in pixels.</summary>
        private const double CellScreenPixels = 8;

        /// <summary>Alpha of the checker over the image.</summary>
        private const float CheckerAlpha = 0.75f;

        /// <summary>Longest side of a region's clip texture, in texels. The clip edge is filtered, so this only has to keep curves smooth.</summary>
        private const int MaxClipSide = 512;

        /// <summary>
        /// Depth test always passes and stencil is off, as for the auto-polygonize overlays: the texture shader writes the depth
        /// and the annotation fills leave a stencil value that would reject it.
        /// </summary>
        private static readonly DepthStencilState DepthState = new()
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false,
            DepthBufferFunction = CompareFunction.Always,
            StencilEnable = false
        };

        private sealed class RegionTexture(Texture2D texture, TextureOverlayView view, XnaVector2 texelCount, double worldPerTexel, XnaVector4 colorA, XnaVector4 colorB) : IDisposable
        {
            public Texture2D Texture { get; } = texture;
            public TextureOverlayView View { get; } = view;
            public XnaVector2 TexelCount { get; } = texelCount;
            public double WorldPerTexel { get; } = worldPerTexel;
            public XnaVector4 ColorA { get; } = colorA;
            public XnaVector4 ColorB { get; } = colorB;

            public void Dispose() => Texture.Dispose();
        }

        private readonly CellOverlapIndex index = new();
        private RegionTexture?[] textures = [];

        /// <summary>The overlap regions as of the last <see cref="Update"/>.</summary>
        public IReadOnlyList<CellOverlapRegion> Regions => index.Regions;

        /// <summary>Recomputes the regions among <paramref name="locations"/> when any outline changed.</summary>
        public void Update(IEnumerable<LocationObj> locations, IVolumeToSectionTransform transform)
        {
            if (!index.Update(locations, transform))
                return;

            DisposeTextures();
            textures = new RegionTexture?[index.Regions.Count];
        }

        /// <summary>The smallest region under <paramref name="position"/> (volume space).</summary>
        public bool TryHit(Geometry.Vector2 position, out CellOverlapRegion? region) => index.TryHit(position, out region);

        public void Draw(GraphicsDevice device, VikingXNA.Scene scene)
        {
            IReadOnlyList<CellOverlapRegion> regions = index.Regions;
            if (regions.Count == 0 || textures.Length != regions.Count)
                return;

            OverlayShaderEffect? effect = DeviceEffectsStore<OverlayShaderEffect>.TryGet(device);
            if (effect is null || !effect.HasTechnique(OverlayShaderEffect.Techniques.CheckerOverlayEffect))
                return;

            DepthStencilState previous = device.DepthStencilState;
            try
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    if (!regions[i].Shape.BoundingBox.Intersects(scene.VisibleWorldBounds))
                        continue;

                    textures[i] ??= CreateTexture(device, regions[i]);
                    RegionTexture? shown = textures[i];
                    if (shown is null)
                        continue;

                    effect.Technique = OverlayShaderEffect.Techniques.CheckerOverlayEffect;
                    effect.StripeClipTexture = shown.Texture;
                    effect.StripeTexelCount = shown.TexelCount;
                    effect.StripeClipLevel = 0.5f;
                    effect.CheckerColorA = shown.ColorA;
                    effect.CheckerColorB = shown.ColorB;
                    effect.CheckerCell = (float)(CellScreenPixels * scene.Camera.Downsample / shown.WorldPerTexel);
                    effect.CheckerAlpha = CheckerAlpha;

                    device.DepthStencilState = DepthState;
                    TextureOverlayView.Draw(device, scene, effect, [shown.View], DepthState);
                }
            }
            finally
            {
                device.DepthStencilState = previous;
            }
        }

        /// <summary>The menu for a right double-click on <paramref name="region"/>: one item per cell that can be given the area.</summary>
        public static ContextMenuStrip CreateMenu(CellOverlapRegion region, SectionViewerControl parent)
        {
            ContextMenuStrip menu = new();
            foreach (long locationId in new[] { region.LocationIdA, region.LocationIdB })
            {
                LocationObj? location = Store.Locations.GetObjectByID(locationId, false);
                string name = location?.Parent is { } structure
                    ? $"{(string.IsNullOrWhiteSpace(structure.Label) ? "Structure" : structure.Label)} {structure.ID}"
                    : $"location {locationId}";
                ToolStripMenuItem give = new($"Give overlap to {name}");
                long winner = locationId;
                give.Click += (_, _) => AssignOverlapAction.TryApply(region, winner, parent);
                menu.Items.Add(give);
            }

            return menu;
        }

        private static RegionTexture? CreateTexture(GraphicsDevice device, CellOverlapRegion region)
        {
            Geometry.Polygon shape = region.Shape;
            Geometry.Rectangle box = shape.BoundingBox;
            double longest = Math.Max(box.Width, box.Height);
            if (!(longest > 0))
                return null;

            // One texel of margin all round keeps the clip edge off the texture border.
            double worldPerTexel = longest / (MaxClipSide - 2);
            int width = (int)Math.Ceiling(box.Width / worldPerTexel) + 2;
            int height = (int)Math.Ceiling(box.Height / worldPerTexel) + 2;
            Geometry.Vector2 lowerLeft = new(box.Left - worldPerTexel, box.LowerLeft.Y - worldPerTexel);
            Geometry.Rectangle bounds = new(lowerLeft, width * worldPerTexel, height * worldPerTexel);
            double top = lowerLeft.Y + (height * worldPerTexel);

            byte[] clip = Rasterize(shape, lowerLeft.X, top, worldPerTexel, width, height);
            Texture2D? texture = null;
            try
            {
                texture = new Texture2D(device, width, height, false, SurfaceFormat.Alpha8);
                texture.SetData(clip);
                TextureOverlayView view = new(texture, bounds, Microsoft.Xna.Framework.Color.White);
                return new RegionTexture(
                    texture,
                    view,
                    new XnaVector2(width, height),
                    worldPerTexel,
                    ColorOf(region.LocationIdA),
                    ColorOf(region.LocationIdB));
            }
            catch (Exception)
            {
                texture?.Dispose();
                return null;
            }
        }

        private static XnaVector4 ColorOf(long locationId)
        {
            LocationObj? location = Store.Locations.GetObjectByID(locationId, false);
            return location?.Parent is { } structure
                ? LocationCanvasView.ColorForStructure(structure, 1f).ToVector4()
                : Microsoft.Xna.Framework.Color.Gray.ToVector4();
        }

        /// <summary>The polygon filled into an alpha mask whose row 0 is its top edge (world Y up, mask rows down).</summary>
        private static byte[] Rasterize(Geometry.Polygon shape, double left, double top, double worldPerTexel, int width, int height)
        {
            using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (GraphicsPath path = new(System.Drawing.Drawing2D.FillMode.Alternate))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(System.Drawing.Color.Transparent);
                PointF[] Ring(Geometry.Vector2[] ring) =>
                    [.. ring.Select(point => new PointF((float)((point.X - left) / worldPerTexel), (float)((top - point.Y) / worldPerTexel)))];

                path.AddPolygon(Ring(shape.ExteriorRing));
                foreach (Geometry.Vector2[] hole in shape.InteriorRings)
                    path.AddPolygon(Ring(hole));

                graphics.FillPath(System.Drawing.Brushes.White, path);
            }

            BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] alpha = new byte[width * height];
                byte[] row = new byte[data.Stride];
                for (int y = 0; y < height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + (y * data.Stride), row, 0, data.Stride);
                    for (int x = 0; x < width; x++)
                        alpha[(y * width) + x] = row[(x * 4) + 3];
                }

                return alpha;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private void DisposeTextures()
        {
            foreach (RegionTexture? texture in textures)
                texture?.Dispose();

            textures = [];
        }

        public void Dispose()
        {
            DisposeTextures();
            index.Clear();
        }
    }
}
