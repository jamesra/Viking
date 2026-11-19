#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using GeometryVector2 = Geometry.Vector2;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The mask the user accepted in Resegment's mask edit, shown dimmed with the dashed outline of the polygon it came from while
    /// the segmentation server works on it, so the step does not look stalled. Short lived: it is shown from the moment the mask
    /// is accepted until <see cref="End"/>, which comes when the server's outline arrives, the request fails or the command
    /// carrying it ends.
    /// </summary>
    /// <remarks>
    /// Owned by what awaits the server call: the <see cref="SegmentationPaintCommand"/> Resegment starts with the accepted mask
    /// (<c>Location_ViewModelBase.SubmitEditedMask</c>). That command draws it in place of its own paint of the same mask until
    /// the first answer, and ends it on that answer (<see cref="SegmentationCommand.OnRequestSettled"/>), on a failure, on a new
    /// stroke or clear, and when it deactivates. The view only draws; it holds no request state. UI (render) thread only.
    /// </remarks>
    internal sealed class ResegmentPendingView : IDisposable
    {
        /// <summary>The mask edit's colours at about half strength, so the pending mask reads as waiting rather than editable.</summary>
        internal static readonly MaskEditPalette DimPalette = new(
            new XnaColor(SegmentationMaskEditCommand.FillColor.R, SegmentationMaskEditCommand.FillColor.G, SegmentationMaskEditCommand.FillColor.B, (byte)35),
            new XnaColor(SegmentationMaskEditCommand.EdgeColor.R, SegmentationMaskEditCommand.EdgeColor.G, SegmentationMaskEditCommand.EdgeColor.B, (byte)130),
            new XnaColor(SegmentationMaskEditCommand.OriginalOutlineColor.R, SegmentationMaskEditCommand.OriginalOutlineColor.G, SegmentationMaskEditCommand.OriginalOutlineColor.B, (byte)150));

        private readonly MaskEditOverlay overlay;

        /// <param name="accepted">The accepted mask, placed where the user left it. Its pixels are not modified.</param>
        /// <param name="originalRings">The polygon Resegment started from (exterior ring, then holes), volume space.</param>
        public ResegmentPendingView(RasterMask accepted, IReadOnlyList<GeometryVector2[]>? originalRings)
        {
            if (accepted is null)
                throw new ArgumentNullException(nameof(accepted));

            overlay = new MaskEditOverlay(accepted.Width, accepted.Height, accepted.Pixels, accepted.WorldBounds, originalRings, DimPalette);
        }

        /// <summary>True until <see cref="End"/>.</summary>
        public bool IsShown { get; private set; } = true;

        /// <summary>Why the view ended, for the diagnostic log; null while it is shown.</summary>
        public string? EndReason { get; private set; }

        /// <summary>
        /// Stops showing the view and frees its texture. Only the first call counts; later ones (a failure after the command
        /// ended, say) do nothing. Returns true when this call ended it, so the caller knows to redraw.
        /// </summary>
        public bool End(string reason)
        {
            if (!IsShown)
                return false;

            IsShown = false;
            EndReason = reason;
            overlay.Dispose();
            SegmentationDiag.Log($"Resegment pending mask removed: {reason}");
            return true;
        }

        /// <summary>Draws the dimmed mask and the dashed original outline while shown; nothing after <see cref="End"/>.</summary>
        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, double downsample)
        {
            if (IsShown)
                overlay.Draw(graphicsDevice, scene, downsample);
        }

        /// <inheritdoc cref="End" />
        public void Dispose() => End("disposed");
    }
}
