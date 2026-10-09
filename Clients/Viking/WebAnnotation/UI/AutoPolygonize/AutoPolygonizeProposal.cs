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
using WebAnnotationModel;
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
        /// Builds overlay data from the highest-scoring segment. The server PNG is a probability
        /// image of the SAM2 logits (128 is the decision boundary), not a 1-bit mask.
        /// Returns null when decode fails. Called off the UI thread; does not create a <see cref="Texture2D"/>.
        /// </summary>
        public static AutoPolygonizeMaskOverlay? TryCreate(
            SegmentationViewportSession session,
            SegmentationServiceTypes.SegmentationResponse response)
        {
            if (session is null || response is null || response.Segments.Count == 0)
                return null;

            var bestSegment = response.GetHighestScoringSegment()!;
            var (decodedMaskData, decodedWidth, decodedHeight) = session.DecodeSegmentMask(bestSegment);
            if (decodedMaskData is null || decodedWidth <= 0 || decodedHeight <= 0)
                return null;

            int inside = 0;
            for (int i = 0; i < decodedMaskData.Length; i++)
            {
                if (decodedMaskData[i] >= SegmentationMaskPolygonizer.SoftMaskForeground)
                    inside++;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[SegmentationProfile] Mask overlay {decodedWidth}x{decodedHeight} pngBytes={bestSegment.Mask.Length} inside={inside}");

            Geometry.Rectangle worldBounds = session.GetSegmentWorldBounds(
                response,
                bestSegment.X,
                bestSegment.Y,
                decodedWidth,
                decodedHeight);
            return new AutoPolygonizeMaskOverlay(decodedMaskData, decodedWidth, decodedHeight, worldBounds);
        }

        /// <summary>
        /// Pixel-wise max (soft-mask OR) of several overlays onto a shared world canvas.
        /// Used when merging same-cell sibling proposals so the debug overlay matches the
        /// unioned ring. Null/empty sources are skipped; a single usable source is returned as-is.
        /// Output size is capped so a wide group cannot allocate an unbounded texture.
        /// </summary>
        public static AutoPolygonizeMaskOverlay? TryOr(IEnumerable<AutoPolygonizeMaskOverlay?>? sources)
        {
            if (sources is null)
                return null;

            List<AutoPolygonizeMaskOverlay> masks = [];
            foreach (AutoPolygonizeMaskOverlay? source in sources)
            {
                if (source is null || source.MaskData is null || source.Width <= 0 || source.Height <= 0)
                    continue;
                if (source.MaskData.Length != source.Width * source.Height)
                    continue;
                masks.Add(source);
            }

            if (masks.Count == 0)
                return null;
            if (masks.Count == 1)
                return masks[0];

            Geometry.Rectangle bounds = masks[0].WorldBounds;
            for (int i = 1; i < masks.Count; i++)
                bounds = Geometry.Rectangle.Union(bounds, masks[i].WorldBounds);

            double worldWidth = Math.Max(bounds.Width, 1e-6);
            double worldHeight = Math.Max(bounds.Height, 1e-6);
            double pixelsPerWorld = masks.Max(mask =>
                Math.Max(mask.Width / Math.Max(mask.WorldBounds.Width, 1e-6),
                    mask.Height / Math.Max(mask.WorldBounds.Height, 1e-6)));

            int width = Math.Max(1, (int)Math.Ceiling(worldWidth * pixelsPerWorld));
            int height = Math.Max(1, (int)Math.Ceiling(worldHeight * pixelsPerWorld));
            const int maxSide = 2048;
            if (width > maxSide || height > maxSide)
            {
                double scale = maxSide / (double)Math.Max(width, height);
                width = Math.Max(1, (int)Math.Floor(width * scale));
                height = Math.Max(1, (int)Math.Floor(height * scale));
            }

            byte[] merged = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                double worldY = bounds.Bottom + ((y + 0.5) / height) * worldHeight;
                for (int x = 0; x < width; x++)
                {
                    double worldX = bounds.Left + ((x + 0.5) / width) * worldWidth;
                    byte value = 0;
                    foreach (AutoPolygonizeMaskOverlay mask in masks)
                    {
                        byte sample = Sample(mask, worldX, worldY);
                        if (sample > value)
                            value = sample;
                    }

                    merged[y * width + x] = value;
                }
            }

            return new AutoPolygonizeMaskOverlay(merged, width, height, bounds);
        }

        private static byte Sample(AutoPolygonizeMaskOverlay mask, double worldX, double worldY)
        {
            Geometry.Rectangle bounds = mask.WorldBounds;
            double w = Math.Max(bounds.Width, 1e-6);
            double h = Math.Max(bounds.Height, 1e-6);
            double nx = (worldX - bounds.Left) / w;
            double ny = (worldY - bounds.Bottom) / h;
            if (nx < 0 || ny < 0 || nx >= 1 || ny >= 1)
                return 0;

            int px = Math.Min(mask.Width - 1, (int)(nx * mask.Width));
            int py = Math.Min(mask.Height - 1, (int)(ny * mask.Height));
            return mask.MaskData[py * mask.Width + px];
        }
    }

    /// <summary>
    /// One raw SAM2 field kept so a later edge-cleanup or hole-drop change can retrace
    /// the ring without another SegmentImage call. <see cref="Mask"/> bytes stay the server
    /// values; cleanup is applied only while tracing.
    /// </summary>
    internal sealed class AutoPolygonizeMaskSource
    {
        public AutoPolygonizeMaskSource(
            AutoPolygonizeMaskOverlay mask,
            IReadOnlyList<Geometry.Vector2>? keepPoints,
            IReadOnlyList<Geometry.Vector2>? preserveHoles)
        {
            Mask = mask ?? throw new ArgumentNullException(nameof(mask));
            KeepPoints = keepPoints is { Count: > 0 } ? [.. keepPoints] : [];
            PreserveHoles = preserveHoles is { Count: > 0 } ? [.. preserveHoles] : [];
        }

        /// <summary>Decoded server probability field. Not replaced when the cleanup radius changes.</summary>
        public AutoPolygonizeMaskOverlay Mask { get; }

        /// <summary>World points that choose which connected piece of <see cref="Mask"/> to trace.</summary>
        public IReadOnlyList<Geometry.Vector2> KeepPoints { get; }

        /// <summary>World points whose holes are kept even when they are under the drop threshold.</summary>
        public IReadOnlyList<Geometry.Vector2> PreserveHoles { get; }

        /// <summary>
        /// Traces this field at <paramref name="edgeCleanupRadius"/>. The mask rectangle is the
        /// viewport, which matches <see cref="SegmentationViewportSession.GetSegmentWorldBounds"/>
        /// for a segment crop (pixel Y is top-origin, world Y is up).
        /// </summary>
        public Polygon? CreatePolygon(double holeDropFraction, int edgeCleanupRadius)
        {
            IReadOnlyList<Polygon> polygons = SegmentationMaskPolygonizer.CreatePolygons(
                Mask.MaskData,
                Mask.Width,
                Mask.Height,
                0,
                0,
                Mask.Width,
                Mask.Height,
                Mask.WorldBounds,
                holeDropFraction,
                PreserveHoles,
                edgeCleanupRadius,
                out _,
                KeepPoints);
            return polygons.FirstOrDefault();
        }
    }

    /// <summary>
    /// Rebuilds a proposal ring and its on-screen mask from stored server fields.
    /// Called when edge cleanup or hole dropping changes, off the UI thread.
    /// </summary>
    internal static class AutoPolygonizeMaskRetrace
    {
        /// <summary>
        /// Polygonizes each source at the new radius, unions those rings with
        /// <paramref name="fixedPolygons"/> (saved shapes that have no mask), and builds
        /// the mask texture from the cleaned fields. Source bytes are not modified.
        /// Returns false when nothing can be traced.
        /// </summary>
        public static bool TryCreate(
            IReadOnlyList<AutoPolygonizeMaskSource>? sources,
            IReadOnlyList<Polygon>? fixedPolygons,
            Geometry.Vector2 keepPoint,
            double holeDropFraction,
            int edgeCleanupRadius,
            double simplifyTolerance,
            out Polygon? polygon,
            out AutoPolygonizeMaskOverlay? displayMask)
        {
            polygon = null;
            displayMask = null;
            if (sources is null || sources.Count == 0)
                return false;

            List<Polygon> parts = [];
            List<AutoPolygonizeMaskOverlay> cleaned = new(sources.Count);
            foreach (AutoPolygonizeMaskSource source in sources)
            {
                if (source?.Mask?.MaskData is null)
                    continue;

                Polygon? traced = source.CreatePolygon(holeDropFraction, edgeCleanupRadius);
                if (traced is not null)
                    parts.Add(AutoPolygonizeSelection.SimplifyProposal(traced, simplifyTolerance));

                byte[] displayBytes = edgeCleanupRadius > 0
                    ? SegmentationMaskPolygonizer.ApplyEdgeCleanup(
                        source.Mask.MaskData,
                        source.Mask.Width,
                        source.Mask.Height,
                        edgeCleanupRadius)
                    : source.Mask.MaskData;
                cleaned.Add(new AutoPolygonizeMaskOverlay(
                    displayBytes,
                    source.Mask.Width,
                    source.Mask.Height,
                    source.Mask.WorldBounds));
            }

            if (fixedPolygons is not null)
            {
                foreach (Polygon fixedPolygon in fixedPolygons)
                {
                    if (fixedPolygon is not null)
                        parts.Add(fixedPolygon);
                }
            }

            if (parts.Count == 0)
                return false;

            Polygon? merged = parts.Count == 1
                ? parts[0]
                : AutoPolygonizeSelection.UnionPolygons(parts, keepPoint);
            if (merged is null)
                return false;

            if (parts.Count > 1)
                merged = AutoPolygonizeSelection.SimplifyProposal(merged, simplifyTolerance);

            polygon = merged;
            displayMask = AutoPolygonizeMaskOverlay.TryOr(cleaned);
            return true;
        }
    }

    /// <summary>
    /// Hollow-line preview of a SAM2 polygon over a circle. Left double-click accepts.
    /// Right/middle double-click opens the ring context menu (Accept / Reject); that menu
    /// is the intended extension point for multi-blob pick and prompt-density prefs later.
    /// When <see cref="Global.AnnotationSettings.AutoPolygonizeOverlayMasks"/> is on, Draw shows
    /// the last SegmentImage mask. Rings stay visible unless that mask is showing and
    /// <see cref="Global.AnnotationSettings.AutoPolygonizeHideSegmentationRings"/> is set.
    /// Prompt dots follow <see cref="Global.AnnotationSettings.AutoPolygonizeOverlayPrompts"/>.
    /// </summary>
    internal sealed class AutoPolygonizeProposal : IHandleMouseDoubleClick, IHelpStrings, IContextMenu
    {
        private const float RingSaturation = 0.80f;
        private const float DefaultLightness = 0.70f;
        private const float HighlightLightness = 0.90f;
        private const float DefaultAlpha = 0.92f;
        private const float HighlightAlpha = 1.0f;
        private const float MaskOverlayAlpha = 0.35f;

        /// <summary>
        /// Depth test always passes and stencil is off. <see cref="DepthStencilState.None"/> drops
        /// the draw: the texture shader writes SV_Depth, and a disabled depth buffer discards those pixels.
        /// Annotation fills also leave a stencil value that would reject the mask over the circle.
        /// </summary>
        private static readonly DepthStencilState MaskOverlayDepthState = new()
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false,
            DepthBufferFunction = CompareFunction.Always,
            StencilEnable = false
        };

        private readonly AutoCirclePolygonizeController controller;
        private readonly AutoPolygonizeMaskOverlay? maskOverlayData;

        /// <summary>
        /// Cleaned copy shown in the mask overlay. Null until a radius change builds one;
        /// the texture then falls back to <see cref="maskOverlayData"/> (the raw server field).
        /// </summary>
        private AutoPolygonizeMaskOverlay? displayMask;

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
            IReadOnlyList<Geometry.Vector2>? backgroundPrompts = null,
            IReadOnlyList<Geometry.Rectangle>? startingBoxes = null,
            IReadOnlyList<AutoPolygonizeMaskSource>? maskSources = null,
            IReadOnlyList<Polygon>? fixedPolygons = null)
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
            StartingBoxes = startingBoxes is { Count: > 0 } ? [.. startingBoxes] : [];
            // A caller that already chose sources (a group union) passes them in.
            // A single SegmentImage passes null and we keep that one raw field.
            MaskSources = maskSources is not null
                ? [.. maskSources.Where(source => source is not null)]
                : maskOverlay is null
                    ? []
                    : [new AutoPolygonizeMaskSource(maskOverlay, ForegroundPrompts, BackgroundPrompts)];
            FixedPolygons = fixedPolygons is { Count: > 0 }
                ? [.. fixedPolygons.Where(shape => shape is not null)]
                : [];
        }

        /// <summary>
        /// Raw server field for the optional debug overlay. Sibling merge ORs these with the
        /// group remask. Null when the SegmentImage had no decodable mask. Edge cleanup does
        /// not replace these bytes; the on-screen texture uses <see cref="SetDisplayMask"/>.
        /// </summary>
        public AutoPolygonizeMaskOverlay? MaskOverlay => maskOverlayData;

        /// <summary>
        /// Raw fields this ring was traced from. Empty when the proposal has no decodable mask
        /// (a refresh then leaves the ring alone). Group proposals flatten each member's sources.
        /// </summary>
        public IReadOnlyList<AutoPolygonizeMaskSource> MaskSources { get; }

        /// <summary>
        /// Rings unioned with <see cref="MaskSources"/> that have no mask of their own,
        /// such as a saved sibling polygon in an overlap group.
        /// </summary>
        public IReadOnlyList<Polygon> FixedPolygons { get; }

        /// <summary>Lowest ID in <see cref="LocationIds"/>; used for color and dictionary lookup.</summary>
        public long LocationId { get; }

        /// <summary>Every location this overlay stands for after a same-cell overlap resubmit, including saved sibling polygons.</summary>
        public IReadOnlyList<long> LocationIds { get; }

        /// <summary>Structure that owns the circles. Null orphans are never grouped.</summary>
        public long? ParentID { get; }

        /// <summary>0 = per-circle mask; 1 = first group resubmit; 2 = one expansion.</summary>
        public int OverlapResubmitRound { get; }

        public int SectionNumber { get; }

        /// <summary>
        /// Ticket of the segmentation request that produced this proposal, assigned before it is published.
        /// See <see cref="RequestSupersession"/>. <see cref="RequestSupersession.Untracked"/> when unset.
        /// </summary>
        public long RequestTicket { get; set; } = RequestSupersession.Untracked;

        public DateTime LastModified { get; }

        public double CircleRadius { get; }

        public Polygon Polygon { get; private set; }

        public IReadOnlyList<CurveView> RingViews { get; private set; }

        /// <summary>
        /// Rebuilds hollow rings after carving or after a cleanup-radius retrace.
        /// The stored server mask is left alone; pass a cleaned field to <see cref="SetDisplayMask"/>
        /// when the on-screen texture should match the new ring.
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

        /// <summary>
        /// Volume-space <c>foreground_boxes</c> from the same SegmentImage (inscribed circle square or
        /// group seed box). Empty when the request had none. Kept so Add points can reopen
        /// <see cref="SegmentationCommand"/> without remasking clicks alone.
        /// </summary>
        public IReadOnlyList<Geometry.Rectangle> StartingBoxes { get; }

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
            "Double right-click: Proposal menu (Accept / Add points / Reject)"
        ];

        /// <summary>
        /// Accept, refine with extra points, or Reject for the ring.
        /// </summary>
        public ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new();
                ToolStripMenuItem accept = new("Accept polygonalization");
                accept.Click += (_, _) => controller.Accept(this);
                menu.Items.Add(accept);

                ToolStripMenuItem addPoints = new("Add points");
                addPoints.Click += (_, _) => controller.BeginAddPoints(this);
                menu.Items.Add(addPoints);

                ToolStripMenuItem reject = new("Reject proposal");
                reject.Click += (_, _) => controller.Dismiss(this);
                menu.Items.Add(reject);
                return menu;
            }
        }

        /// <summary>
        /// Shows <see cref="ContextMenu"/> at the cursor. Used when preferential overlay
        /// double-click hits the ring (annotation still owns single-click under the ring).
        /// </summary>
        public void ShowContextMenuAtCursor()
        {
            ContextMenu.Show(Control.MousePosition);
        }

        public bool HandleMouseDoubleClick(MouseButtons button, Geometry.Vector2 worldPosition)
        {
            if (button == MouseButtons.Left)
            {
                controller.Accept(this);
                return true;
            }

            // Right/middle: DefaultCommand opens IContextMenu when this is ObjectAtPosition.
            // Preferential overlay hits call ShowContextMenuAtCursor instead.
            return false;
        }

        public bool TryHit(Geometry.Vector2 worldPosition, double worldThreshold, out double distance)
        {
            distance = AutoPolygonizeSelection.DistanceToAnyRing(Polygon, worldPosition);
            return distance <= worldThreshold;
        }

        /// <summary>
        /// True when decoded SAM2 bytes were kept. The GPU texture may still be missing
        /// until <see cref="EnsureMaskOverlay"/> runs on the graphics thread.
        /// </summary>
        public bool HasMaskSource => maskOverlayData?.MaskData is { Length: > 0 };

        /// <summary>
        /// Creates the mask texture when bytes were kept and the GPU view is missing.
        /// No-op without mask bytes. Must run on the graphics thread.
        /// </summary>
        public void EnsureMaskOverlay(GraphicsDevice? graphicsDevice)
        {
            if (maskOverlayView is not null || !HasMaskSource)
                return;

            AttachMaskOverlay(graphicsDevice);
        }

        /// <summary>
        /// Swaps the on-screen mask for a cleaned field built from the stored originals.
        /// Pass null to show the raw server field again. Must run on the graphics thread.
        /// Does nothing to the GPU texture until the overlay is on or a texture already exists.
        /// </summary>
        public void SetDisplayMask(AutoPolygonizeMaskOverlay? mask, GraphicsDevice? graphicsDevice)
        {
            displayMask = mask;
            if (graphicsDevice is null)
                return;

            if (maskTexture is null && !Global.AnnotationSettings.AutoPolygonizeOverlayMasks)
                return;

            AttachMaskOverlay(graphicsDevice);
        }

        /// <summary>
        /// Draws rings first, then the mask, then prompt dots. The mask is on top of the outline
        /// so a tight ring cannot cover it. Rings are skipped when masks are visible and
        /// <see cref="Global.AnnotationSettings.AutoPolygonizeHideSegmentationRings"/> is set.
        /// </summary>
        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            if (Global.AnnotationSettings.AutoPolygonizeShowSegmentationRings)
            {
                double lineWidth = AutoPolygonizeSelection.ProposalLineWidth(CircleRadius, scene.Camera.Downsample);
                foreach (CurveView ringView in RingViews)
                {
                    ringView.LineWidth = lineWidth;
                    ringView.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
                }
            }

            if (Global.AnnotationSettings.AutoPolygonizeOverlayMasks)
            {
                EnsureMaskOverlay(graphicsDevice);
                DrawMaskOverlay(graphicsDevice, scene);
            }

            if (Global.AnnotationSettings.AutoPolygonizeOverlayPrompts)
                DrawPromptOverlays(graphicsDevice, scene);
        }

        /// <summary>
        /// Draws the SAM2 texture with the depth test forced to pass and stencil disabled.
        /// </summary>
        private void DrawMaskOverlay(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            if (maskOverlayView is null)
                return;

            DepthStencilState previous = graphicsDevice.DepthStencilState;
            graphicsDevice.DepthStencilState = MaskOverlayDepthState;
            try
            {
                maskOverlayView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, MaskOverlayDepthState);
            }
            finally
            {
                graphicsDevice.DepthStencilState = previous;
            }
        }

        /// <summary>
        /// Green foreground / red background circles matching interactive Segment. Depth is
        /// disabled so the dots sit above annotations. Radius tracks live downsample.
        /// Drawn only when <see cref="Global.AnnotationSettings.AutoPolygonizeOverlayPrompts"/> is on.
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
            AutoPolygonizeMaskOverlay? shown = displayMask ?? maskOverlayData;
            if (graphicsDevice is null || shown?.MaskData is null)
                return;

            Color color = ColorForLocation(LocationId).SetAlpha(MaskOverlayAlpha);
            maskTexture = CreateMaskTexture(graphicsDevice, shown, color);
            if (maskTexture is null)
                return;

            maskOverlayView = new TextureOverlayView(maskTexture, shown.WorldBounds, color);
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
        /// Packs the probability mask into a Color texture. Values on the inside of the
        /// logit-zero boundary use <paramref name="color"/>; the outside halo stays transparent.
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
                    pixels[i] = overlay.MaskData[i] >= SegmentationMaskPolygonizer.SoftMaskForeground
                        ? color
                        : Color.Transparent;

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
        /// Ring color for a location. A cell uses that structure's hue with the ring lightness and alpha. Other locations keep a stable hue from the location id so a refresh does not recolor the ring. Highlighted (cursor over the ring) is lighter and fully opaque.
        /// </summary>
        public static Color ColorForLocation(long locationId, bool highlighted = false)
        {
            float lightness = highlighted ? HighlightLightness : DefaultLightness;
            float alpha = highlighted ? HighlightAlpha : DefaultAlpha;
            LocationObj location = Store.Locations.GetObjectByID(locationId, false);
            if (location?.Parent is { TypeID: 1 } cell)
            {
                return SegmentationDistinctColors.ColorFromHsl(
                    HueOf(cell.Color.ToXNAColor(1f)), RingSaturation, lightness, alpha);
            }

            float hue = (float)((locationId * 0.6180339887) % 1.0);
            return SegmentationDistinctColors.ColorFromHsl(hue, RingSaturation, lightness, alpha);
        }

        /// <summary>
        /// Hue in 0..1 from an RGB color. Autoseg uses this so a cell ring matches <see cref="StructureObj.Color"/> while keeping ring lightness.
        /// </summary>
        private static float HueOf(Color color)
        {
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;
            if (delta <= 0f)
                return 0f;

            float hue = max == r
                ? ((g - b) / delta) % 6f
                : max == g
                    ? ((b - r) / delta) + 2f
                    : ((r - g) / delta) + 4f;
            hue /= 6f;
            return hue < 0f ? hue + 1f : hue;
        }
    }
}
