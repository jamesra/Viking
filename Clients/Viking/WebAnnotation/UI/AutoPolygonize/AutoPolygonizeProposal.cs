using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands.Segmentation;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Decoded SAM2 mask plus world bounds for the optional debug overlay.
    /// GPU texture creation stays on <see cref="AutoPolygonizeProposal.AttachMaskOverlay"/>.
    /// </summary>
    internal sealed class AutoPolygonizeMaskOverlay
    {
        public byte[] MaskData { get; }
        public int Width { get; }
        public int Height { get; }
        public GridRectangle WorldBounds { get; }

        public AutoPolygonizeMaskOverlay(byte[] maskData, int width, int height, GridRectangle worldBounds)
        {
            MaskData = maskData;
            Width = width;
            Height = height;
            WorldBounds = worldBounds;
        }

        /// <summary>
        /// Builds overlay data from the highest-scoring segment. Returns null when decode fails.
        /// Called off the UI thread; does not create a <see cref="Texture2D"/>.
        /// </summary>
        public static AutoPolygonizeMaskOverlay? TryCreate(
            SegmentationViewportSession session,
            SegmentationServiceTypes.SegmentationResponse response)
        {
            if (session is null || response is null || response.Segments.Count == 0)
                return null;

            var bestSegment = response.Segments.OrderByDescending(segment => segment.Score).First();
            var (decodedMaskData, decodedWidth, decodedHeight) = session.DecodePngMask(bestSegment.Mask.ToByteArray());
            if (decodedMaskData is null || decodedWidth <= 0 || decodedHeight <= 0)
                return null;

            GridRectangle worldBounds = session.GetSegmentWorldBounds(
                bestSegment.X,
                bestSegment.Y,
                decodedWidth,
                decodedHeight,
                response.Width,
                response.Height);
            return new AutoPolygonizeMaskOverlay(decodedMaskData, decodedWidth, decodedHeight, worldBounds);
        }
    }

    /// <summary>
    /// Hollow-line preview of a SAM2 polygon over a circle. Double-click accepts; right/middle dismisses.
    /// The mask overlay is drawn only when <see cref="Global.AnnotationSettings.AutoPolygonizeOverlayMasks"/> is on.
    /// </summary>
    internal sealed class AutoPolygonizeProposal : IHandleMouseDoubleClick, IHelpStrings
    {
        private const float RingSaturation = 0.80f;
        private const float DefaultLightness = 0.70f;
        private const float HighlightLightness = 0.90f;
        private const float DefaultAlpha = 0.92f;
        private const float HighlightAlpha = 1.0f;
        private const float MaskOverlayAlpha = 0.35f;

        private readonly AutoCirclePolygonizeController controller;
        private readonly AutoPolygonizeMaskOverlay? maskOverlayData;
        private Texture2D maskTexture;
        private TextureOverlayView maskOverlayView;
        private bool isHighlighted;

        public AutoPolygonizeProposal(
            AutoCirclePolygonizeController controller,
            long locationId,
            int sectionNumber,
            DateTime lastModified,
            double circleRadius,
            GridPolygon polygon,
            IReadOnlyList<CurveView> ringViews,
            AutoPolygonizeMaskOverlay? maskOverlay = null)
        {
            this.controller = controller;
            LocationId = locationId;
            SectionNumber = sectionNumber;
            LastModified = lastModified;
            CircleRadius = circleRadius;
            Polygon = polygon;
            RingViews = ringViews;
            maskOverlayData = maskOverlay;
        }

        public long LocationId { get; }

        public int SectionNumber { get; }

        public DateTime LastModified { get; }

        public double CircleRadius { get; }

        public GridPolygon Polygon { get; }

        public IReadOnlyList<CurveView> RingViews { get; }

        public bool IsHighlighted
        {
            get => isHighlighted;
            set
            {
                if (isHighlighted == value)
                    return;

                isHighlighted = value;
                Color color = ColorForLocation(LocationId, isHighlighted);
                foreach (CurveView ringView in RingViews)
                    ringView.Color = color;
            }
        }

        public string[] HelpStrings =>
        [
            "Double-click: Accept polygonalization",
            "Double right-click: Dismiss segmentation overlay"
        ];

        public bool HandleMouseDoubleClick(MouseButtons button, GridVector2 worldPosition)
        {
            if (button == MouseButtons.Left)
            {
                controller.Accept(this);
                return true;
            }

            if (button == MouseButtons.Right || button == MouseButtons.Middle)
            {
                controller.Dismiss(this);
                return true;
            }

            return false;
        }

        public bool TryHit(GridVector2 worldPosition, double worldThreshold, out double distance)
        {
            distance = AutoPolygonizeSelection.DistanceToAnyRing(Polygon, worldPosition);
            return distance <= worldThreshold;
        }

        /// <summary>
        /// Draws the debug mask first so the hollow rings stay readable on top.
        /// </summary>
        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            if (Global.AnnotationSettings.AutoPolygonizeOverlayMasks)
                maskOverlayView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);

            double lineWidth = AutoPolygonizeSelection.ProposalLineWidth(CircleRadius, scene.Camera.Downsample);
            foreach (CurveView ringView in RingViews)
            {
                ringView.LineWidth = lineWidth;
                ringView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }
        }

        /// <summary>
        /// Creates the mask <see cref="Texture2D"/> on the graphics thread. Safe to call more than once.
        /// </summary>
        public void AttachMaskOverlay(GraphicsDevice graphicsDevice)
        {
            DisposeMaskOverlay();
            if (graphicsDevice is null || maskOverlayData?.MaskData is null)
                return;

            Color color = ColorForLocation(LocationId).SetAlpha(MaskOverlayAlpha);
            maskTexture = CreateMaskTexture(graphicsDevice, maskOverlayData, color);
            if (maskTexture is null)
                return;

            maskOverlayView = new TextureOverlayView(maskTexture, maskOverlayData.WorldBounds, color);
        }

        /// <summary>
        /// Releases the GPU texture. Called when the proposal is removed or overlay preference turns off.
        /// </summary>
        public void DisposeMaskOverlay()
        {
            maskOverlayView = null;
            maskTexture?.Dispose();
            maskTexture = null;
        }

        /// <summary>
        /// Packs the 1-bit mask into a Color texture: foreground uses <paramref name="color"/>, background is transparent.
        /// </summary>
        private static Texture2D CreateMaskTexture(
            GraphicsDevice graphicsDevice,
            AutoPolygonizeMaskOverlay overlay,
            Color color)
        {
            if (overlay.MaskData.Length != overlay.Width * overlay.Height)
                return null;

            try
            {
                Texture2D texture = new(graphicsDevice, overlay.Width, overlay.Height);
                Color[] pixels = new Color[overlay.MaskData.Length];
                for (int i = 0; i < overlay.MaskData.Length; i++)
                    pixels[i] = overlay.MaskData[i] > 0 ? color : Color.Transparent;

                texture.SetData(pixels);
                return texture;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Builds exterior and hole <see cref="CurveView"/>s using the circle-resize line width.
        /// </summary>
        public static IReadOnlyList<CurveView> CreateRingViews(
            GridPolygon polygon,
            Color color,
            double circleRadius,
            double downsample)
        {
            double lineWidth = AutoPolygonizeSelection.ProposalLineWidth(circleRadius, downsample);
            Color ringColor = color;
            List<CurveView> rings =
            [
                CreateRingView(polygon.ExteriorRing, ringColor, lineWidth)
            ];

            foreach (GridVector2[] hole in polygon.InteriorRings)
                rings.Add(CreateRingView(hole, ringColor, lineWidth));

            return rings;
        }

        private static CurveView CreateRingView(ICollection<GridVector2> ring, Color color, double lineWidth) =>
            new(
                ring,
                color,
                TryToClose: true,
                numInterpolations: 0,
                lineWidth: lineWidth,
                lineStyle: LineStyle.Tubular,
                ShowControlPoints: false);

        /// <summary>
        /// Stable hue from location ID so the same circle keeps the same color across refreshes.
        /// Highlighted (cursor over the ring) uses a lighter, fully opaque color.
        /// </summary>
        public static Color ColorForLocation(long locationId, bool highlighted = false)
        {
            float hue = (float)((locationId * 0.6180339887) % 1.0);
            float lightness = highlighted ? HighlightLightness : DefaultLightness;
            float alpha = highlighted ? HighlightAlpha : DefaultAlpha;
            return ColorFromHsl(hue, RingSaturation, lightness, alpha);
        }

        private static Color ColorFromHsl(float hue, float saturation, float lightness, float alpha)
        {
            hue -= (float)Math.Floor(hue);
            float q = lightness < 0.5f
                ? lightness * (1 + saturation)
                : lightness + saturation - lightness * saturation;
            float p = 2 * lightness - q;
            float r = HueToRgb(p, q, hue + 1f / 3f);
            float g = HueToRgb(p, q, hue);
            float b = HueToRgb(p, q, hue - 1f / 3f);
            return new Color(r, g, b, alpha);
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }
}
