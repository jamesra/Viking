#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI.Commands;
using Viking.UI.Controls;
using VikingXNAGraphics;
using GeometryVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Shows a mask over the section and lets the user grow or shrink it (dilate or erode) with the mouse wheel. It is an image
    /// operation on a bitmap and nothing more: it does not know about segmentation or the server and does not touch any annotation.
    /// Another command supplies the input and consumes the output.
    /// </summary>
    /// <remarks>
    /// Input is a <see cref="RasterMask"/> (a rasterized polygon comes from <see cref="RasterMask.FromRings"/>). Output is
    /// a <see cref="RasterMask"/> too, delivered once: the edited mask when the user accepts (Enter or the pen's check
    /// button), or null when they cancel (Escape, the pen's X, or any other way the command is deactivated).
    /// Wheel up grows, wheel down shrinks; Shift takes quarter steps; Ctrl+wheel zooms the view as usual. Holding the left
    /// button and moving the mouse moves the whole mask; a double-click on the mask accepts it, like Enter.
    /// The edit is one number, the radius, applied to a signed distance field computed at activation
    /// (<see cref="SegmentationMaskEdit"/>), so a tick re-thresholds the bitmap and re-uploads one texture; there is no
    /// polygon rebuild. All members are used on the UI thread.
    /// </remarks>
    internal sealed class SegmentationMaskEditCommand : Command, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        private const double DefaultStepNanometers = 20;

        private readonly RasterMask input;
        private readonly Action<RasterMask?> onFinished;
        private readonly SegmentationPenButtons penButtons;
        private readonly PenCancelButton cancelButton;

        private float[]? signedDistance;
        private readonly byte[] editedPixels;
        private readonly Microsoft.Xna.Framework.Color[] colors;
        private Texture2D? texture;
        private TextureOverlayView? overlayView;
        private bool textureDirty = true;
        private double minRadius;
        private double maxRadius;
        private double radiusPixels;
        private bool finished;

        /// <summary>Where the user has dragged the mask, in whole mosaic pixels from where it came in (multiples of the raster scale).</summary>
        private int offsetColumns;
        private int offsetRows;
        private bool dragging;
        private Geometry.Vector2 dragStartWorld;
        private int dragStartColumns;
        private int dragStartRows;

        /// <summary>Colour and opacity of the mask overlay (straight, not premultiplied, alpha).</summary>
        public static readonly Microsoft.Xna.Framework.Color OverlayColor = new(255, 190, 0, 120);

        /// <param name="parent">The viewer the command runs in.</param>
        /// <param name="mask">The mask to edit. Not modified.</param>
        /// <param name="onFinished">
        /// Called once, on the UI thread: with the edited mask on accept, null on cancel. An accepted result is never empty.
        /// </param>
        public SegmentationMaskEditCommand(SectionViewerControl parent, RasterMask mask, Action<RasterMask?> onFinished)
            : base(parent)
        {
            input = mask ?? throw new ArgumentNullException(nameof(mask));
            editedPixels = new byte[mask.Pixels.Length];
            colors = new Microsoft.Xna.Framework.Color[mask.Pixels.Length];
            this.onFinished = onFinished ?? throw new ArgumentNullException(nameof(onFinished));
            penButtons = new SegmentationPenButtons(parent);
            cancelButton = new PenCancelButton(parent);
            StepNanometers = DefaultStepNanometers;
        }

        /// <summary>One wheel notch in nanometres. A notch is never less than one mask pixel.</summary>
        public double StepNanometers { get; set; }

        /// <summary>Current edit in mask pixels: positive grows the input, negative shrinks it, zero is the input.</summary>
        public double RadiusPixels => radiusPixels;

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
        /// <summary>Help lines shown while the command is active.</summary>
        public string[] HelpStrings =>
        [
            "Scroll wheel: Grow (up) or shrink (down) the mask",
            "Shift + scroll wheel: Fine steps",
            "Hold Left click + Drag: Move the mask",
            "Double-click the mask: Accept the mask",
            "Ctrl + scroll wheel: Zoom",
            "Enter or the green check: Accept the mask",
            "Escape or the red X: Cancel",
            "Hold Right click + Drag: Move view",
        ];

        /// <inheritdoc />
        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);
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
            overlayView = null;
            texture?.Dispose();
            texture = null;

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

            double nmPerWorld = Global.Scale.X > 0 ? Global.Scale.X : 1;
            double change = SegmentationMaskEdit.RadiusChange(
                e.Delta / 120.0,
                StepNanometers / nmPerWorld,
                input.WorldPerPixel,
                (Control.ModifierKeys & Keys.Shift) != 0);
            ApplyRadius(radiusPixels + change);
        }

        /// <inheritdoc />
        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
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

        /// <inheritdoc />
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
                    if (overlayView is not null)
                        overlayView.BoundingRect = Placed().WorldBounds;
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

        /// <inheritdoc />
        protected override void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                Accept();
                e.Handled = true;
                return;
            }

            base.OnKeyPress(sender, e);
        }

        /// <summary>Clamps <paramref name="radius"/>, re-thresholds the bitmap and marks the texture for upload.</summary>
        private void ApplyRadius(double radius)
        {
            if (signedDistance is null)
                return;

            radiusPixels = Math.Max(minRadius, Math.Min(maxRadius, radius));
            SegmentationMaskEdit.Threshold(signedDistance, radiusPixels, editedPixels);
            textureDirty = true;
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

        /// <summary>Copies the bitmap into the texture, creating it on first use. Render thread.</summary>
        private void UploadTexture(GraphicsDevice device)
        {
            if (texture is null || texture.IsDisposed)
            {
                texture?.Dispose();
                texture = new Texture2D(device, input.Width, input.Height);
                overlayView = new TextureOverlayView(texture, Placed().WorldBounds, Microsoft.Xna.Framework.Color.White);
            }

            for (int i = 0; i < editedPixels.Length; i++)
                colors[i] = editedPixels[i] >= SegmentationMaskEdit.Inside ? OverlayColor : Microsoft.Xna.Framework.Color.Transparent;

            texture.SetData(colors);
            textureDirty = false;
        }

        /// <inheritdoc />
        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (finished || signedDistance is null)
                return;

            if (textureDirty || texture is null || texture.IsDisposed)
                UploadTexture(graphicsDevice);

            overlayView?.Draw(graphicsDevice, scene, OverlayStyle.Alpha);

            DepthStencilState previousDepth = graphicsDevice.DepthStencilState;
            graphicsDevice.DepthStencilState = DepthStencilState.None;
            penButtons.Draw(graphicsDevice, scene, includeSkip: false);
            cancelButton.Draw(graphicsDevice, scene);
            graphicsDevice.DepthStencilState = previousDepth;
        }
    }
}
