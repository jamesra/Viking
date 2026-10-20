using System;
using System.Windows.Input;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotation.Tools
{
    /// <summary>A tool that uses the mouse wheel itself instead of letting the view zoom.</summary>
    public interface IAnnotationToolWheel
    {
        /// <summary>Returns true when the tool used the wheel; false lets the view zoom.</summary>
        bool OnMouseWheel(int delta, bool shift, bool ctrl);
    }

    /// <summary>
    /// Viking's "Dilate/erode mask": the polygon's raster mask is shown as an overlay, the mouse wheel grows (up) or shrinks
    /// (down) it, Shift for fine steps and Ctrl to zoom instead. Enter accepts the mask and hands it on (Viking then
    /// segments from it); Escape cancels.
    /// </summary>
    public sealed class MaskEditTool : IAnnotationTool, IAnnotationToolPreview, IAnnotationToolWheel
    {
        /// <summary>Viking's default wheel step, in nanometers.</summary>
        const double StepNanometers = 20;

        readonly AnnotationToolContext _context;
        readonly RasterMask _input;
        readonly Action<RasterMask> _onAccepted;
        readonly float[] _signedDistance;
        readonly byte[] _edited;
        readonly Microsoft.Xna.Framework.Color[] _colors;
        readonly double _minRadius;
        readonly double _maxRadius;
        double _radiusPixels;
        Texture2D _texture;
        TextureOverlayView _overlay;
        bool _textureDirty = true;
        bool _finished;

        internal MaskEditTool(AnnotationToolContext context, RasterMask mask, Action<RasterMask> onAccepted)
        {
            _context = context;
            _input = mask ?? throw new ArgumentNullException(nameof(mask));
            _onAccepted = onAccepted ?? throw new ArgumentNullException(nameof(onAccepted));
            _edited = new byte[mask.Pixels.Length];
            _colors = new Microsoft.Xna.Framework.Color[mask.Pixels.Length];
            _signedDistance = SegmentationMaskEdit.SignedDistance(mask.Pixels, mask.Width, mask.Height);
            if (_signedDistance != null)
            {
                _minRadius = SegmentationMaskEdit.MinRadius(_signedDistance);
                _maxRadius = SegmentationMaskEdit.MaxRadius(mask.Pixels, mask.Width, mask.Height);
                ApplyRadius(0);
            }
        }

        /// <summary>False when the mask has no inside to grow or shrink; the host should not start the tool.</summary>
        internal bool IsUsable => _signedDistance != null;

        public string StatusText =>
            $"Mask: wheel grows or shrinks ({_radiusPixels * _input.WorldPerPixel * (Global.Scale.X > 0 ? Global.Scale.X : 1):+0;-0;0} nm), Shift for fine steps, Enter accepts, Esc cancels";

        public bool OnMouseWheel(int delta, bool shift, bool ctrl)
        {
            if (ctrl || _finished)
                return false;

            double nmPerWorld = Global.Scale.X > 0 ? Global.Scale.X : 1;
            double change = SegmentationMaskEdit.RadiusChange(delta / 120.0, StepNanometers / nmPerWorld, _input.WorldPerPixel, shift);
            ApplyRadius(_radiusPixels + change);
            _context.SetStatus(StatusText);
            return true;
        }

        public AnnotationToolResult OnMouseDown(Vector2 screen, MouseButton button, int clickCount) => AnnotationToolResult.Continue;

        public void OnMouseMove(Vector2 screen)
        {
        }

        public void OnMouseUp()
        {
        }

        public AnnotationToolResult OnKeyDown(Key key)
        {
            switch (key)
            {
                case Key.Escape:
                    Cancel();
                    return AnnotationToolResult.Done;
                case Key.Enter:
                    if (_finished || _signedDistance == null)
                        return AnnotationToolResult.Continue;
                    _finished = true;
                    byte[] pixels = new byte[_edited.Length];
                    Array.Copy(_edited, pixels, pixels.Length);
                    ReleaseTexture();
                    _onAccepted(_input.WithPixels(pixels));
                    return AnnotationToolResult.Done;
                default:
                    return AnnotationToolResult.Continue;
            }
        }

        public void Cancel()
        {
            _finished = true;
            ReleaseTexture();
            _context.SetStatus(null);
            _context.Invalidate();
        }

        void ApplyRadius(double radius)
        {
            _radiusPixels = Math.Max(_minRadius, Math.Min(_maxRadius, radius));
            SegmentationMaskEdit.Threshold(_signedDistance, _radiusPixels, _edited);
            _textureDirty = true;
            _context.Invalidate();
        }

        void ReleaseTexture()
        {
            _overlay = null;
            _texture?.Dispose();
            _texture = null;
        }

        public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect)
        {
            if (_finished || _signedDistance == null)
                return;

            if (_textureDirty || _texture == null || _texture.IsDisposed)
            {
                if (_texture == null || _texture.IsDisposed)
                {
                    _texture = new Texture2D(device, _input.Width, _input.Height);
                    _overlay = new TextureOverlayView(_texture, _input.WorldBounds, Microsoft.Xna.Framework.Color.White);
                }

                for (int i = 0; i < _edited.Length; i++)
                    _colors[i] = _edited[i] >= SegmentationMaskEdit.Inside ? SegmentationMaskEditCommand.OverlayColor : Microsoft.Xna.Framework.Color.Transparent;
                _texture.SetData(_colors);
                _textureDirty = false;
            }

            _overlay?.Draw(device, scene, OverlayStyle.Alpha);
        }
    }
}
