#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.Services.Grpc;
using Viking.UI;
using Viking.UI.Controls;
using VikingXNAGraphics;
using GVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Manual segmentation by painting. The user paints the object with a round brush (foreground), paints what is not the object
    /// (background) or erases paint; a moment after the last button is released the painted masks are sent to the segmentation server
    /// and the polygon that comes back is shown. Painting more sends the new masks and the shown polygon is replaced when the answer
    /// arrives. Accepting the polygon saves it as an annotation through the same callback as <see cref="SegmentationCommand"/>.
    /// </summary>
    /// <remarks>
    /// Mouse: press the left button over unpainted ground to paint foreground and drag to continue. Shift+left paints background; Shift+left
    /// started over paint erases the type of paint nearest the cursor until the button is released. The brush grows and shrinks with the
    /// sideways wheel, or Shift plus the ordinary wheel. Pen: the same with the pen's barrel button (see <see cref="BarrelButtonBinding"/>)
    /// in place of Shift, the on-screen check button to accept and the red X to cancel. Double-clicking the result, Enter and the check
    /// button accept; Escape and the red X cancel. The point placing <see cref="SegmentationCommand"/> it derives from is retired; this command replaces it. Everything here runs on the UI thread; the server request and the polygonizing are the base class's.
    /// </remarks>
    internal sealed class SegmentationPaintCommand : SegmentationCommand, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        /// <summary>Quiet time after the last button release before the masks are sent.</summary>
        public const int SubmitPauseMilliseconds = 350;

        private const double DefaultRadiusPixels = 24;
        private const int MaxCanvasSide = 3072;
        private const int RingSegments = 48;

        /// <summary>Colour of the foreground paint (straight, not premultiplied, alpha).</summary>
        public static readonly Microsoft.Xna.Framework.Color ForegroundColor = new(0, 200, 60, 120);

        /// <summary>Colour of the background paint (straight, not premultiplied, alpha).</summary>
        public static readonly Microsoft.Xna.Framework.Color BackgroundColor = new(230, 40, 40, 120);

        private readonly PenCancelButton paintCancelButton;
        private PaintCanvas? canvas;
        private Texture2D? texture;
        private TextureOverlayView? overlayView;
        private PanZoomDebouncer? submitDebouncer;
        private double radiusPixels = DefaultRadiusPixels;

        private bool painting;
        private PaintMode mode;
        private GVector2 lastStrokeWorld;
        private bool paintedSinceSubmit;

        private GVector2? cursorWorld;
        private CurveView? ringView;
        private (GVector2 Center, double Radius, PaintMode Mode, bool Painting)? ringKey;
        private System.Collections.Generic.IReadOnlyList<GVector2> lastForegroundClicks = [];

        /// <param name="parent">The viewer the command runs in.</param>
        /// <param name="success_callback">Receives the accepted polygon, volume space.</param>
        /// <param name="grpcChannelManager">The segmentation service channel; null uses the selected service.</param>
        /// <param name="seedRings">
        /// An existing mask to start from, as polygon rings in volume space (exterior ring first, then holes). It is painted as foreground
        /// when the command starts and segmented at once, so its result is shown before the user touches anything. Null starts empty.
        /// </param>
        /// <param name="onAbandoned">Runs when the command ends without an accepted polygon (Escape, the red X, another command).</param>
        public SegmentationPaintCommand(
            SectionViewerControl parent,
            OnCommandSuccess? success_callback = null,
            IGrpcChannelManager? grpcChannelManager = null,
            System.Collections.Generic.IReadOnlyList<GVector2[]>? seedRings = null,
            Action? onAbandoned = null)
            : base(parent, success_callback, grpcChannelManager, onAbandoned: onAbandoned)
        {
            paintCancelButton = new PenCancelButton(parent);
            ShowPenButtons = Global.ShowPenActionButtons;
            this.seedRings = seedRings;
        }

        private readonly System.Collections.Generic.IReadOnlyList<GVector2[]>? seedRings;

        /// <inheritdoc />
        protected override bool PlacesPoints => false;

        /// <summary>The brush radius in screen pixels.</summary>
        public double RadiusPixels => radiusPixels;

        #region Help Strings
        /// <summary>Help lines shown while the command is active.</summary>
        public static readonly string[] PaintHelpStrings =
        [
            "Left-click + drag: Paint foreground (the object)",
            "Shift + Left-click + drag: Paint background (not the object)",
            "Shift + Left-click on paint + drag: Erase the nearest kind of paint",
            "Pen: the barrel button works like Shift; the check button accepts, the red X cancels",
            "Sideways wheel or Shift + wheel: Brush size",
            "Double-click the result or press Enter: Accept",
            "Escape or the red X: Cancel",
            "Hold Right click + Drag: Move view",
        ];

        /// <summary>Help lines shown while the command is active.</summary>
        public new string[] HelpStrings => PaintHelpStrings;

        /// <inheritdoc cref="HelpStrings" />
        public new ObservableCollection<string> ObservableHelpStrings => new(PaintHelpStrings);
        #endregion

        #region Lifecycle
        /// <inheritdoc />
        public override void OnActivate()
        {
            base.OnActivate();

            int downsample = Math.Max(1, (int)Math.Ceiling(Parent.Camera?.Downsample ?? Parent.Downsample));
            int maxSide = Math.Min(Parent.MaxTextureSide, MaxCanvasSide);
            Geometry.Rectangle area = Parent.Scene.VisibleWorldBounds;
            if (seedRings is { Count: > 0 })
                area = Geometry.Rectangle.Union(area, RasterMask.BoundsOf(seedRings[0]));

            canvas = PaintCanvas.ForView(area, downsample, maxSide);
            submitDebouncer = new PanZoomDebouncer(SubmitPauseMilliseconds, SubmitPaint, action => Viking.UI.State.MainThreadDispatcher?.BeginInvoke(action));

            if (seedRings is { Count: > 0 } && RasterMask.FromRings(seedRings, canvas.Downsample, 0, maxSide) is RasterMask seed)
            {
                canvas.Fill(seed, PaintCanvas.Foreground);
                SubmitPaint();
            }
        }

        /// <inheritdoc />
        protected override void OnDeactivate()
        {
            submitDebouncer?.Dispose();
            submitDebouncer = null;
            overlayView = null;
            texture?.Dispose();
            texture = null;
            base.OnDeactivate();
        }
        #endregion

        #region Painting
        /// <summary>Brush radius in volume units, never smaller than most of a canvas pixel so a stroke always marks something.</summary>
        private double RadiusWorld =>
            Math.Max(radiusPixels * Parent.Downsample, (canvas?.WorldPerPixel ?? 1) * 0.75);

        /// <summary>
        /// Starts a stroke at <paramref name="world"/>. <paramref name="alternate"/> is Shift or the barrel button: it makes the stroke
        /// background, or an erase when it starts over paint (see <see cref="PaintBrush.ChooseMode"/>).
        /// </summary>
        private void BeginStroke(GVector2 world, bool alternate)
        {
            if (canvas is null || IsFinished)
                return;

            mode = PaintBrush.ChooseMode(canvas, world, RadiusWorld, alternate);
            painting = true;
            lastStrokeWorld = world;
            cursorWorld = world;
            ContinueStroke(world);
        }

        /// <summary>Paints from where the stroke last was to <paramref name="world"/>.</summary>
        private void ContinueStroke(GVector2 world)
        {
            if (canvas is null || !painting)
                return;

            if (PaintBrush.Apply(canvas, mode, lastStrokeWorld, world, RadiusWorld) > 0)
                paintedSinceSubmit = true;

            lastStrokeWorld = world;
            cursorWorld = world;
            Parent.Invalidate();
        }

        /// <summary>Ends the stroke; the masks are sent after a pause if anything was painted since the last send.</summary>
        private void EndStroke()
        {
            if (!painting)
                return;

            painting = false;
            if (paintedSinceSubmit)
                submitDebouncer?.Restart();

            Parent.Invalidate();
        }

        /// <summary>
        /// Sends the painted masks to the segmentation server, or clears the result when no foreground is left. Runs on the UI thread
        /// from the pause timer; does nothing while a new stroke is under way, since its release restarts the pause.
        /// </summary>
        private void SubmitPaint()
        {
            if (canvas is null || IsFinished || painting)
                return;

            paintedSinceSubmit = false;
            RasterMask? foreground = canvas.ToMask(PaintCanvas.Foreground);
            SegmentationSeed? seed = foreground is null ? null : SegmentationSeed.FromMask(foreground);
            if (seed is null || foreground is null)
            {
                lastForegroundClicks = [];
                SetPromptMasks(null, null);
                RequestSegmentationFromPrompts();
                return;
            }

            if (foreground.TryGetSetWorldBounds(out Geometry.Rectangle setBounds) &&
                SegmentationMaskTiles.TilesSpanned(setBounds, foreground.Downsample) > SegmentationMaskTiles.MaxForegroundTiles)
            {
                SegmentationUserFeedback.NotifySkip(
                    Parent,
                    SegmentationSkipKind.Error,
                    $"The painted object is too large for the segmentation server (at most {SegmentationMaskTiles.MaxForegroundTiles} tiles). Zoom out and paint a smaller area.");
                return;
            }

            SegmentationMaskUnion? background = null;
            RasterMask? backgroundMask = canvas.ToMask(PaintCanvas.Background);
            if (backgroundMask is not null && SegmentationSeed.FromMask(backgroundMask) is SegmentationSeed backgroundSeed)
            {
                background = new SegmentationMaskUnion();
                background.Add(backgroundSeed);
            }

            // One click inside each separate piece of paint; pieces that touch or overlap are one piece and share one click.
            System.Collections.Generic.List<GVector2> clicks = canvas.InteriorPoints(PaintCanvas.Foreground);
            lastForegroundClicks = clicks;
            SetPromptMasks(seed, background, clicks);
            RequestSegmentationFromPrompts();
        }

        /// <summary>
        /// The polygon the user most likely means: the largest result that covers a foreground click, otherwise the largest.
        /// Null when there is no result yet.
        /// </summary>
        private Geometry.Polygon? BestPolygon()
        {
            var polygons = ResultPolygons;
            if (polygons.Count == 0)
                return null;

            return polygons.FirstOrDefault(polygon => lastForegroundClicks.Any(click => polygon.Covers(click))) ?? polygons[0];
        }

        /// <summary>Overlapping polygons of one answer are shown, and accepted, as one (see <see cref="SegmentationResultMerge"/>).</summary>
        protected override System.Collections.Generic.IReadOnlyList<Geometry.Polygon> MergeResultPolygons(
            System.Collections.Generic.IReadOnlyList<Geometry.Polygon> polygons,
            System.Collections.Generic.IReadOnlyList<GVector2> foreground) =>
            SegmentationResultMerge.MergeOverlapping(polygons, foreground);

        /// <inheritdoc />
        protected override void AcceptBestPolygon()
        {
            if (BestPolygon() is Geometry.Polygon polygon)
                AcceptPolygon(polygon);
        }
        #endregion

        #region Mouse
        private bool HitsButton(int x, int y) =>
            paintCancelButton.HitTest(x, y) ||
            (ShowPenButtons && SegmentationPenButtons.HitTest(false, Parent.ClientSize.Width, x, y, Parent.DockedChromeInsets) is not null);

        /// <inheritdoc />
        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !IsFinished && !HitsButton(e.X, e.Y))
            {
                BeginStroke(Parent.ScreenToWorld(e.X, e.Y), (Control.ModifierKeys & Keys.Shift) != 0);
                return;
            }

            base.OnMouseDown(sender, e);
        }

        /// <inheritdoc />
        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GVector2 world = Parent.ScreenToWorld(e.X, e.Y);
            cursorWorld = world;
            if (painting && e.Button == MouseButtons.Left)
            {
                ContinueStroke(world);
                Parent.StatusPosition = world;
                return;
            }

            base.OnMouseMove(sender, e);
            Parent.Invalidate();
        }

        /// <inheritdoc />
        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                EndStroke();

            base.OnMouseUp(sender, e);
        }

        /// <summary>A double-click on the result accepts it. A click that is not on a result does nothing.</summary>
        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !IsFinished)
            {
                GVector2 world = Parent.ScreenToWorld(e.X, e.Y);
                Geometry.Polygon? hit = ResultPolygons.FirstOrDefault(polygon => polygon.Covers(world));
                if (hit is null && canvas?.ValueAt(world) == PaintCanvas.Foreground)
                    hit = BestPolygon();

                if (hit is not null)
                {
                    painting = false;
                    AcceptPolygon(hit);
                    return;
                }
            }

            base.OnMouseDoubleClick(sender, e);
        }

        /// <summary>Shift + wheel sizes the brush; the wheel alone zooms as in every command.</summary>
        protected override void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                ResizeBrush(e.Delta / 120.0);
                return;
            }

            base.OnMouseWheel(sender, e);
        }

        /// <summary>The sideways wheel sizes the brush: right grows it, left shrinks it.</summary>
        protected override void OnMouseHWheel(object sender, MouseEventArgs e) => ResizeBrush(e.Delta / 120.0);

        private void ResizeBrush(double notches)
        {
            radiusPixels = PaintBrush.ResizeRadius(radiusPixels, notches);
            Parent.Invalidate();
        }

        /// <inheritdoc />
        protected override void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                AcceptBestPolygon();
                e.Handled = true;
                return;
            }

            base.OnKeyPress(sender, e);
        }
        #endregion

        #region Pen
        /// <summary>
        /// A touch on a button acts on the button; a touch elsewhere starts a stroke, with the barrel button standing in for Shift.
        /// The pen's eraser end keeps its usual job of moving the view.
        /// </summary>
        protected override void OnPenContact(object sender, PenEventArgs e)
        {
            ShowPenButtons = true;
            if (IsFinished || e.Erase)
            {
                base.OnPenContact(sender, e);
                return;
            }

            if (paintCancelButton.HitTest(e.X, e.Y))
            {
                CancelCommand();
                return;
            }

            if (SegmentationPenButtons.HitTest(false, Parent.ClientSize.Width, e.X, e.Y, Parent.DockedChromeInsets) == SegmentationPenButton.Accept)
            {
                AcceptBestPolygon();
                return;
            }

            BeginStroke(Parent.ScreenToWorld(e.X, e.Y), e.Barrel);
        }

        /// <inheritdoc />
        protected override void OnPenLeaveContact(object sender, PenEventArgs e)
        {
            EndStroke();
            base.OnPenLeaveContact(sender, e);
        }

        /// <summary>
        /// Moves the brush ring and continues a stroke. The base command's cancel on a barrel press is not wanted here: the barrel
        /// button is a painting modifier.
        /// </summary>
        protected override void OnPenMove(object sender, PenEventArgs e)
        {
            if (e.Erase)
            {
                base.OnPenMove(sender, e);
                return;
            }

            GVector2 world = Parent.ScreenToWorld(e.X, e.Y);
            Parent.StatusPosition = world;
            SaveAsOldPenPosition(e);
            cursorWorld = world;
            if (painting && e.InContact)
                ContinueStroke(world);
            else
                Parent.Invalidate();
        }
        #endregion

        #region Rendering
        /// <summary>Copies the pixels changed since the last frame into the display texture, creating it on first use. Render thread.</summary>
        private void UploadChangedPaint(GraphicsDevice device)
        {
            if (canvas is null)
                return;

            if (texture is null || texture.IsDisposed)
            {
                texture?.Dispose();
                texture = new Texture2D(device, canvas.Width, canvas.Height);
                overlayView = new TextureOverlayView(texture, canvas.WorldBounds, Microsoft.Xna.Framework.Color.White);
            }

            if (!canvas.TakeDirtyRegion(out int column, out int row, out int width, out int height))
                return;

            Microsoft.Xna.Framework.Color[] colors = new Microsoft.Xna.Framework.Color[width * height];
            for (int y = 0; y < height; y++)
            {
                int source = (row + y) * canvas.Width + column;
                int target = y * width;
                for (int x = 0; x < width; x++)
                {
                    byte value = canvas.Pixels[source + x];
                    colors[target + x] = value == PaintCanvas.Foreground
                        ? ForegroundColor
                        : value == PaintCanvas.Background ? BackgroundColor : Microsoft.Xna.Framework.Color.Transparent;
                }
            }

            texture.SetData(0, new Microsoft.Xna.Framework.Rectangle(column, row, width, height), colors, 0, colors.Length);
        }

        /// <summary>The brush outline, rebuilt only when its place, size or colour changed.</summary>
        private CurveView? BrushRing()
        {
            if (cursorWorld is not GVector2 center)
                return null;

            double radius = RadiusWorld;
            var key = (center, radius, mode, painting);
            if (ringKey != key || ringView is null)
            {
                Microsoft.Xna.Framework.Color color = !painting
                    ? Microsoft.Xna.Framework.Color.White
                    : mode switch
                    {
                        PaintMode.Foreground => Microsoft.Xna.Framework.Color.LimeGreen,
                        PaintMode.Background => Microsoft.Xna.Framework.Color.Red,
                        _ => Microsoft.Xna.Framework.Color.Yellow,
                    };

                GVector2[] ring = new GVector2[RingSegments];
                for (int i = 0; i < ring.Length; i++)
                {
                    double angle = 2 * Math.PI * i / ring.Length;
                    ring[i] = new GVector2(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
                }

                ringView = new CurveView(
                    ring,
                    color.SetAlpha(0.9f),
                    TryToClose: true,
                    numInterpolations: 0,
                    lineWidth: Math.Max(1.0, 2.0 * Parent.Downsample),
                    lineStyle: LineStyle.Tubular,
                    ShowControlPoints: false);
                ringKey = key;
            }

            return ringView;
        }

        /// <inheritdoc />
        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (IsFinished)
                return;

            UploadChangedPaint(graphicsDevice);
            overlayView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            base.OnDraw(graphicsDevice, scene, basicEffect);

            DepthStencilState previousDepth = graphicsDevice.DepthStencilState;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            BrushRing()?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            graphicsDevice.DepthStencilState = previousDepth;
        }
        #endregion
    }
}
