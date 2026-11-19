#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.Common;
using Viking.UI;
using Viking.UI.Commands;
using Viking.UI.Controls;
using VikingXNAGraphics;
using WebAnnotation.UI.AutoPolygonize;
using GeometryVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Shows a mask over the section and lets the user grow or shrink it (dilate or erode) with the mouse wheel. It is an image
    /// operation on a bitmap and nothing more: it does not know about segmentation or the server and does not touch any annotation.
    /// Another command supplies the input and consumes the output.
    /// </summary>
    /// <remarks>
    /// Input is a <see cref="RasterMask"/> (a rasterized polygon comes from <see cref="RasterMask.FromRings"/>) and, optionally, the
    /// rings of the polygon it came from. Output is a <see cref="RasterMask"/> too, delivered once: the edited mask when the user
    /// accepts, or null when they cancel (Escape, a red X, or any other way the command is deactivated).
    /// Wheel up grows, wheel down shrinks; Shift takes quarter steps; Ctrl+wheel zooms the view as usual. Holding the left
    /// button and moving the mouse moves the whole mask; a double-click on the mask accepts it, like Enter and the green checks.
    /// The mask is drawn as a translucent teal fill with an opaque edge (<see cref="FillColor"/>, <see cref="EdgeColor"/>), the
    /// original polygon as a dashed line (<see cref="OriginalOutlineColor"/>), both by a <see cref="MaskEditOverlay"/>. Beside the
    /// mask sit a label with the signed change in nm and what to do next, and a green check and red X that act on one click
    /// (<see cref="MaskEditChromeLayout"/>); the corner pen buttons stay as well.
    /// The edit is one number, the radius, applied to a signed distance field computed at activation
    /// (<see cref="SegmentationMaskEdit"/>), so a tick re-thresholds the bitmap and re-uploads one texture; there is no
    /// polygon rebuild. All members are used on the UI thread.
    /// </remarks>
    internal sealed class SegmentationMaskEditCommand : Command, IHelp, IObservableHelp
    {
        /// <summary>Shown after "Active command" in the help window.</summary>
        public override string Purpose => "Edit segmentation mask";

        private const double DefaultStepNanometers = 20;

        private readonly RasterMask input;
        private readonly Action<RasterMask?> onFinished;
        private readonly SegmentationPenButtons penButtons;
        private readonly PenCancelButton cancelButton;
        private readonly MaskEditOverlay overlay;
        private readonly ProposalButtonsView besideButtons = new();
        private LabelView? label;

        private float[]? signedDistance;
        private readonly byte[] editedPixels;
        private double minRadius;
        private double maxRadius;
        private double radiusPixels;
        private bool finished;

        /// <summary>The set pixels of the edit before the drag offset, volume space; null when none are set.</summary>
        private Geometry.Rectangle? unmovedSetBounds;

        /// <summary>The button beside the mask under the cursor, drawn highlighted.</summary>
        private ProposalButtonKind? hoveredButton;

        /// <summary>Where the user has dragged the mask, in whole mosaic pixels from where it came in (multiples of the raster scale).</summary>
        private int offsetColumns;
        private int offsetRows;
        private bool dragging;
        private Geometry.Vector2 dragStartWorld;
        private int dragStartColumns;
        private int dragStartRows;

        /// <summary>
        /// Fill of the mask (straight alpha): a light teal, low alpha so the tissue shows through, and a hue that is neither the
        /// yellow of an annotation being moved nor the green and red of the paint brush.
        /// </summary>
        public static readonly Microsoft.Xna.Framework.Color FillColor = new(0, 200, 225, 70);

        /// <summary>The mask's edge pixels: the fill's hue, brighter and opaque, so the edge reads on grey TEM at any zoom.</summary>
        public static readonly Microsoft.Xna.Framework.Color EdgeColor = new(0, 235, 255, 255);

        /// <summary>The dashed outline of the polygon the mask came from: white, nearly opaque.</summary>
        public static readonly Microsoft.Xna.Framework.Color OriginalOutlineColor = new(255, 255, 255, 220);

        /// <summary>The colours the edit draws with; <see cref="ResegmentPendingView.DimPalette"/> is the dimmed version.</summary>
        internal static readonly MaskEditPalette Palette = new(FillColor, EdgeColor, OriginalOutlineColor);

        /// <param name="parent">The viewer the command runs in.</param>
        /// <param name="mask">The mask to edit. Not modified.</param>
        /// <param name="onFinished">
        /// Called once, on the UI thread: with the edited mask on accept, null on cancel. An accepted result is never empty.
        /// </param>
        /// <param name="originalOutline">
        /// The polygon the mask came from (exterior ring, then holes), volume space, drawn dashed so the edit can be compared with it.
        /// It stays where it is when the mask is dragged. Null draws none.
        /// </param>
        public SegmentationMaskEditCommand(SectionViewerControl parent, RasterMask mask, Action<RasterMask?> onFinished,
            IReadOnlyList<GeometryVector2[]>? originalOutline = null)
            : base(parent)
        {
            input = mask ?? throw new ArgumentNullException(nameof(mask));
            editedPixels = new byte[mask.Pixels.Length];
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));
            penButtons = new SegmentationPenButtons(parent);
            cancelButton = new PenCancelButton(parent);
            overlay = new MaskEditOverlay(mask.Width, mask.Height, editedPixels, mask.WorldBounds, originalOutline, Palette);
            StepNanometers = DefaultStepNanometers;
        }

        /// <summary>One wheel notch in nanometres. A notch is never less than one mask pixel.</summary>
        public double StepNanometers { get; set; }

        /// <summary>Current edit in mask pixels: positive grows the input, negative shrinks it, zero is the input.</summary>
        public double RadiusPixels => radiusPixels;

        /// <summary>Nanometres per volume unit, as the wheel step is measured.</summary>
        private static double NanometersPerWorld => Global.Scale.X > 0 ? Global.Scale.X : 1;

        /// <summary>The current edit in nanometres, signed as <see cref="RadiusPixels"/>.</summary>
        public double ChangeNanometers => SegmentationMaskEdit.ChangeNanometers(radiusPixels, input.WorldPerPixel, NanometersPerWorld);

        /// <summary>The input mask moved by the user's drag and edited by <see cref="RadiusPixels"/>.</summary>
        public RasterMask CurrentMask()
        {
            byte[] pixels = new byte[editedPixels.Length];
            Array.Copy(editedPixels, pixels, pixels.Length);
            return Placed().WithPixels(pixels);
        }

        /// <summary>The input's placement after the drag; its pixels are the input's, not the edit.</summary>
        private RasterMask Placed() => input.Translated(offsetColumns, offsetRows);

        #region Help Strings
        /// <summary>The wheel, drag, click and double-click gestures; the keys come from <see cref="KeyTable"/>.</summary>
        public static readonly HelpEntry[] MouseHelp =
        [
            HelpEntry.For(HelpInput.Wheel, "Grow (up) or shrink (down) the mask"),
            HelpEntry.For(HelpInput.Wheel, "Fine steps", HelpModifiers.Shift),
            HelpEntry.For(HelpInput.LeftDrag, "Move the mask"),
            HelpEntry.For(HelpInput.LeftDoubleClick, "Accept the mask", where: "on the mask"),
            HelpEntry.For(HelpInput.LeftClick, "Accept (green check) or cancel (red X)", where: "on a button beside the mask or in the corner"),
            HelpEntry.For(HelpInput.Wheel, "Zoom", HelpModifiers.Ctrl),
            HelpEntry.For(HelpInput.RightDrag, "Move view"),
        ];

        /// <summary>Help shown while the command is active.</summary>
        public HelpEntry[] Help => [.. MouseHelp, .. KeyHelp];

        /// <inheritdoc />
        public ObservableCollection<HelpEntry> ObservableHelp => new(Help);

        private KeyBinding[]? _maskEditKeyTable;

        /// <summary>
        /// Enter accepts (as the green checks do) and Escape cancels (as the red X buttons do), then the default keys without their
        /// Escape row so the help lists Escape once. Enter with Shift accepts as before; with Ctrl or Alt it does not.
        /// </summary>
        protected override IReadOnlyList<KeyBinding> KeyTable => _maskEditKeyTable ??=
        [
            new KeyBinding(HelpEntry.For(HelpInput.Enter, "Accept the mask (or a green check)"),
                e => { if (e.Control || e.Alt) return false; Accept(); return true; }, AnyModifiers: true),
            KeyBinding.Do(HelpEntry.For(HelpInput.Escape, "Cancel (or a red X)"), _ => CancelCommand()),
            .. DefaultKeyTable.Where(b => !(b.Help.Inputs is { Length: 1 } inputs && inputs[0] == HelpInput.Escape)),
        ];
        #endregion

        /// <inheritdoc />
        public override void OnActivate()
        {
            base.OnActivate();

            signedDistance = SegmentationMaskEdit.SignedDistance(input.Pixels, input.Width, input.Height);
            if (signedDistance is null)
            {
                // Nothing to edit; report it as a cancel rather than showing an empty overlay.
                CancelCommand();
                return;
            }

            minRadius = SegmentationMaskEdit.MinRadius(signedDistance);
            maxRadius = SegmentationMaskEdit.MaxRadius(input.Pixels, input.Width, input.Height);
            ApplyRadius(0);
        }

        /// <inheritdoc />
        protected override void OnDeactivate()
        {
            overlay.Dispose();

            if (!finished)
            {
                finished = true;
                onFinished(null);
            }

            base.OnDeactivate();
        }

        /// <inheritdoc />
        protected override void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                base.OnMouseWheel(sender, e);
                return;
            }

            double change = SegmentationMaskEdit.RadiusChange(
                e.Delta / 120.0,
                StepNanometers / NanometersPerWorld,
                input.WorldPerPixel,
                (Control.ModifierKeys & Keys.Shift) != 0);
            ApplyRadius(radiusPixels + change);
        }

        /// <summary>
        /// A left press on a button beside the mask or in the corner acts on it at once; anywhere else it starts moving the mask.
        /// The buttons are tested first so a click on one never starts a drag.
        /// </summary>
        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                switch (Chrome()?.HitTest(e.X, e.Y))
                {
                    case ProposalButtonKind.Accept:
                        Accept();
                        return;
                    case ProposalButtonKind.Hide:
                        CancelCommand();
                        return;
                }

                if (cancelButton.HitTest(e.X, e.Y))
                {
                    CancelCommand();
                    return;
                }

                if (SegmentationPenButtons.HitTest(includeSkip: false, Parent.ClientSize.Width, e.X, e.Y, Parent.DockedChromeInsets) is SegmentationPenButton.Accept)
                {
                    Accept();
                    return;
                }

                dragging = true;
                dragStartWorld = Parent.ScreenToWorld(e.X, e.Y);
                dragStartColumns = offsetColumns;
                dragStartRows = offsetRows;
                return;
            }

            base.OnMouseDown(sender, e);
        }

        /// <summary>Moves the mask while dragging; otherwise highlights the button beside the mask under the cursor.</summary>
        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Geometry.Vector2 now = Parent.ScreenToWorld(e.X, e.Y);
                double worldPerBlock = input.WorldPerPixel;
                int scale = input.Scale;
                // Whole raster blocks, so the mask stays on the mosaic pixel grid the server's tiles are cut from.
                int columns = dragStartColumns + (int)Math.Round((now.X - dragStartWorld.X) / worldPerBlock) * scale;
                int rows = dragStartRows + (int)Math.Round((now.Y - dragStartWorld.Y) / worldPerBlock) * scale;
                if (columns != offsetColumns || rows != offsetRows)
                {
                    offsetColumns = columns;
                    offsetRows = rows;
                    overlay.SetWorldBounds(Placed().WorldBounds);
                    Parent.Invalidate();
                }
            }
            else if (!finished)
            {
                ProposalButtonKind? hovered = Chrome()?.HitTest(e.X, e.Y);
                if (hovered != hoveredButton)
                {
                    hoveredButton = hovered;
                    Parent.Invalidate();
                }
            }

            base.OnMouseMove(sender, e);
        }

        /// <inheritdoc />
        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                dragging = false;

            base.OnMouseUp(sender, e);
        }

        /// <inheritdoc />
        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left &&
                signedDistance is not null &&
                CurrentMask().Contains(Parent.ScreenToWorld(e.X, e.Y)))
            {
                dragging = false;
                Accept();
                return;
            }

            base.OnMouseDoubleClick(sender, e);
        }

        /// <summary>Clamps <paramref name="radius"/>, re-thresholds the bitmap and marks the texture for upload.</summary>
        private void ApplyRadius(double radius)
        {
            if (signedDistance is null)
                return;

            radiusPixels = Math.Max(minRadius, Math.Min(maxRadius, radius));
            SegmentationMaskEdit.Threshold(signedDistance, radiusPixels, editedPixels);
            overlay.SetPixels(editedPixels);
            unmovedSetBounds = input.WithPixels(editedPixels).TryGetSetWorldBounds(out Geometry.Rectangle set) ? set : null;
            Parent.Invalidate();
        }

        private void Accept()
        {
            if (finished || signedDistance is null)
                return;

            finished = true;
            onFinished(CurrentMask());
            Execute();
        }

        /// <summary>
        /// The volume-space rectangle the chrome keeps clear of: the edited mask's set pixels where the drag has put them, and the
        /// original outline. Null before the first edit.
        /// </summary>
        private Geometry.Rectangle? ChromeWorldBounds()
        {
            Geometry.Rectangle? bounds = null;
            if (unmovedSetBounds is Geometry.Rectangle set)
            {
                // Translated moves the raster by whole mosaic pixels, Downsample volume units each, rows up.
                double dx = offsetColumns * (double)input.Downsample, dy = offsetRows * (double)input.Downsample;
                bounds = new Geometry.Rectangle(set.Left + dx, set.Right + dx, set.Bottom + dy, set.Top + dy);
            }

            if (overlay.OriginalBounds is Geometry.Rectangle original)
                bounds = bounds is Geometry.Rectangle sofar ? Geometry.Rectangle.Union(sofar, original) : original;

            return bounds;
        }

        /// <summary>The label text for the current edit.</summary>
        private string LabelText() => SegmentationMaskEdit.LabelText(ChangeNanometers);

        /// <summary>
        /// Where the buttons and label beside the mask are now, in screen pixels, laid out from the mask's projected bounds; null
        /// before the first edit or once the command finished. The same layout serves drawing and hit testing.
        /// </summary>
        private MaskEditChrome? Chrome()
        {
            if (finished || signedDistance is null || ChromeWorldBounds() is not Geometry.Rectangle world)
                return null;

            VikingXNA.Scene scene = Parent.Scene;
            GeometryVector2[] corners =
            [
                scene.WorldToScreen(world.Left, world.Bottom), scene.WorldToScreen(world.Right, world.Bottom),
                scene.WorldToScreen(world.Left, world.Top), scene.WorldToScreen(world.Right, world.Top),
            ];
            ScreenBox box = new(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
            return MaskEditChromeLayout.Place(box, Parent.ClientSize.Width, Parent.ClientSize.Height, Parent.DockedChromeInsets, LabelWidthPixels(LabelText()));
        }

        /// <summary>Screen width of <paramref name="text"/> at <see cref="MaskEditChromeLayout.LabelFontPixels"/>.</summary>
        private double LabelWidthPixels(string text)
        {
            SpriteFont? font = Parent.fontArial;
            if (font is null || font.LineSpacing <= 0)
                return text.Length * MaskEditChromeLayout.LabelFontPixels * 0.55;

            return font.MeasureString(text).X * MaskEditChromeLayout.LabelFontPixels / font.LineSpacing;
        }

        /// <summary>Draws the label and the two buttons of <paramref name="chrome"/>, screen positions mapped to the world each frame.</summary>
        private void DrawChrome(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, MaskEditChrome chrome)
        {
            double downsample = Parent.Downsample;
            ProposalButtonPlacement placement = new(
                Parent.ScreenToWorld(chrome.Accept.X, chrome.Accept.Y),
                Parent.ScreenToWorld(chrome.Cancel.X, chrome.Cancel.Y),
                chrome.ButtonRadius * downsample,
                Fade: 1,
                InsideCircle: false,
                ProposalButtonArrangement.TopToBottom);
            besideButtons.Add(placement, _ => ProposalButtonPhase.Idle, hoveredButton, 1f);
            besideButtons.Flush(graphicsDevice, scene);

            if (Parent.spriteBatch is null || Parent.fontArial is null)
                return;

            label ??= new LabelView(string.Empty, GeometryVector2.Zero, Microsoft.Xna.Framework.Color.White, Alignment.CenterCenter, Anchor.CenterCenter, scaleFontWithScene: true)
            {
                OutlineStyleOverride = LabelOutlineStyle.WhiteTextBlackOutline,
            };
            string text = LabelText();
            if (label.Text != text)
                label.Text = text;
            label.Position = Parent.ScreenToWorld(chrome.LabelCenter.X, chrome.LabelCenter.Y);
            label.FontSize = MaskEditChromeLayout.LabelFontPixels * downsample;
            LabelView.Draw(Parent.spriteBatch, Parent.fontArial, scene, [label]);
        }

        /// <inheritdoc />
        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (finished || signedDistance is null)
                return;

            overlay.Draw(graphicsDevice, scene, Parent.Downsample);

            DepthStencilState previousDepth = graphicsDevice.DepthStencilState;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            if (Chrome() is MaskEditChrome chrome)
                DrawChrome(graphicsDevice, scene, chrome);
            penButtons.Draw(graphicsDevice, scene, includeSkip: false);
            cancelButton.Draw(graphicsDevice, scene);
            graphicsDevice.DepthStencilState = previousDepth;
        }
    }
}
