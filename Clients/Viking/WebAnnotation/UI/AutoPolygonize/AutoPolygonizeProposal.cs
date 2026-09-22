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

using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

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
        public Geometry.Rectangle WorldBounds { get; }

        public AutoPolygonizeMaskOverlay(byte[] maskData, int width, int height, Geometry.Rectangle worldBounds)
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

            Geometry.Rectangle worldBounds = session.GetSegmentWorldBounds(
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
    /// When <see cref="Global.AnnotationSettings.AutoPolygonizeOverlayMasks"/> is on, Draw also
    /// shows the last SegmentImage mask and the green/red prompts that produced it.
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
        private PointSetView? foregroundPointsView;
        private PointSetView? backgroundPointsView;
        private bool isHighlighted;

        public AutoPolygonizeProposal(
            AutoCirclePolygonizeController controller,
            long locationId,
            int sectionNumber,
            DateTime lastModified,
            double circleRadius,
            Polygon polygon,
            IReadOnlyList<CurveView> ringViews,
            AutoPolygonizeMaskOverlay? maskOverlay = null,
            IReadOnlyList<long>? locationIds = null,
            long? parentId = null,
            int overlapResubmitRound = 0,
            IReadOnlyList<Geometry.Vector2>? foregroundPrompts = null,
            IReadOnlyList<Geometry.Vector2>? backgroundPrompts = null)
        {
            this.controller = controller;
            LocationIds = locationIds is { Count: > 0 }
                ? [.. locationIds.Distinct().OrderBy(id => id)]
                : [locationId];
            LocationId = LocationIds[0];
            ParentID = parentId;
            OverlapResubmitRound = overlapResubmitRound;
            SectionNumber = sectionNumber;
            LastModified = lastModified;
            CircleRadius = circleRadius;
            Polygon = polygon;
            RingViews = ringViews;
            maskOverlayData = maskOverlay;
            ForegroundPrompts = foregroundPrompts is { Count: > 0 } ? [.. foregroundPrompts] : [];
            BackgroundPrompts = backgroundPrompts is { Count: > 0 } ? [.. backgroundPrompts] : [];
        }

        /// <summary>Lowest ID in <see cref="LocationIds"/>; used for color and dictionary lookup.</summary>
        public long LocationId { get; }

        /// <summary>Every location this overlay stands for after a same-cell overlap resubmit, including saved sibling polygons.</summary>
        public IReadOnlyList<long> LocationIds { get; }

        /// <summary>Structure that owns the circles. Null orphans are never grouped.</summary>
        public long? ParentID { get; }

        /// <summary>0 = per-circle mask; 1 = first group resubmit; 2 = one expansion.</summary>
        public int OverlapResubmitRound { get; }

        public int SectionNumber { get; }

        public DateTime LastModified { get; }

        public double CircleRadius { get; }

        public Polygon Polygon { get; private set; }

        public IReadOnlyList<CurveView> RingViews { get; private set; }

        /// <summary>
        /// Rebuilds hollow rings after carving against a newly accepted annotation.
        /// The SAM2 mask overlay is left alone so debug view still shows the original mask.
        /// </summary>
        public void ReplacePolygon(Polygon polygon, double downsample)
        {
            Polygon = polygon;
            RingViews = CreateRingViews(
                polygon,
                ColorForLocation(LocationId, isHighlighted),
                CircleRadius,
                downsample);
        }

        /// <summary>Volume-space SAM2 label-1 clicks from the SegmentImage that produced this overlay.</summary>
        public IReadOnlyList<Geometry.Vector2> ForegroundPrompts { get; }

        /// <summary>Volume-space SAM2 label-0 clicks from the same request. Drawn red in mask-debug mode.</summary>
        public IReadOnlyList<Geometry.Vector2> BackgroundPrompts { get; }

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

        public bool HandleMouseDoubleClick(MouseButtons button, Geometry.Vector2 worldPosition)
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

        public bool TryHit(Geometry.Vector2 worldPosition, double worldThreshold, out double distance)
        {
            distance = AutoPolygonizeSelection.DistanceToAnyRing(Polygon, worldPosition);
            return distance <= worldThreshold;
        }

        /// <summary>
        /// Draws the debug mask first, then rings, then the last-sent prompts so clicks
        /// stay readable on top of the mask. Prompt dots follow
        /// <see cref="Global.AnnotationSettings.AutoPolygonizeOverlayMasks"/> like the mask.
        /// </summary>
        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            bool showDebugOverlay = Global.AnnotationSettings.AutoPolygonizeOverlayMasks;
            if (showDebugOverlay)
                maskOverlayView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);

            double lineWidth = AutoPolygonizeSelection.ProposalLineWidth(CircleRadius, scene.Camera.Downsample);
            foreach (CurveView ringView in RingViews)
            {
                ringView.LineWidth = lineWidth;
                ringView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }

            if (showDebugOverlay)
                DrawPromptOverlays(graphicsDevice, scene);
        }

        /// <summary>
        /// Green foreground / red background circles matching interactive Segment. Depth is
        /// disabled so the dots sit on the mask. Radius tracks live downsample.
        /// </summary>
        private void DrawPromptOverlays(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            double radius = Global.AnnotationSettings.SegmentationPointRadius * scene.Camera.Downsample;
            EnsurePromptView(ref backgroundPointsView, BackgroundPrompts, Color.Red, radius);
            EnsurePromptView(ref foregroundPointsView, ForegroundPrompts, Color.Green, radius);

            DepthStencilState previous = graphicsDevice.DepthStencilState;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            backgroundPointsView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            foregroundPointsView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            graphicsDevice.DepthStencilState = previous;
        }

        private static void EnsurePromptView(
            ref PointSetView? view,
            IReadOnlyList<Geometry.Vector2> points,
            Color color,
            double radius)
        {
            if (points is null || points.Count == 0)
                return;

            if (view is null)
            {
                view = new PointSetView(color, radius)
                {
                    Points = [.. points]
                };
                return;
            }

            if (Math.Abs(view.PointRadius - radius) > 1e-6)
                view.PointRadius = radius;
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
        /// Builds exterior and hole <see cref="CurveView"/>s using the circle-resize line width
        /// and Catmull-Rom display interpolations so the overlay matches a saved CURVEPOLYGON.
        /// </summary>
        public static IReadOnlyList<CurveView> CreateRingViews(
            Polygon polygon,
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

            foreach (Geometry.Vector2[] hole in polygon.InteriorRings)
                rings.Add(CreateRingView(hole, ringColor, lineWidth));

            return rings;
        }

        private static CurveView CreateRingView(ICollection<Geometry.Vector2> ring, Color color, double lineWidth) =>
            new(
                ring,
                color,
                TryToClose: true,
                numInterpolations: Global.NumClosedCurveInterpolationPointsForDisplay,
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
