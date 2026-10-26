using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI.Controls;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// What <see cref="SegmentationViewportSession"/> needs from the view it segments: the camera, the section, channel
    /// and transforms that identify the tiles, and a way to render a tile of mosaic to a texture. Viking's section viewer
    /// and Jotunn's scene host both provide it. Members are read on the UI thread.
    /// </summary>
    public interface ISegmentationViewport
    {
        /// <summary>The camera's downsample (volume pixels per screen pixel).</summary>
        double CameraDownsample { get; }

        int ViewportWidth { get; }

        int ViewportHeight { get; }

        Geometry.Vector2 ScreenToWorld(double x, double y);

        /// <summary>The section being viewed, or 0 when none is.</summary>
        int SectionNumber { get; }

        string VolumeName { get; }

        /// <summary>
        /// True when world coordinates are the volume, the section placed by its volume transform; false when they are the
        /// section's own mosaic. Tiles, prompts and results of a request are all in this space.
        /// </summary>
        bool InVolumeSpace { get; }

        /// <summary>The StosGroup placing the section in the volume; empty in mosaic space (<see cref="InVolumeSpace"/> false).</summary>
        string VolumeTransform { get; }

        string SectionTransform { get; }

        /// <summary>
        /// The channel part of the tile key: everything besides volume, section, transforms and position that changes
        /// what a capture shows (for Viking's viewer, the channel set and the view options drawn into captures). The
        /// server shares tiles between clients by key, so two different pictures must never get the same value.
        /// </summary>
        string Channel { get; }

        /// <summary>True when one channel is shown, so captures are encoded as grayscale.</summary>
        bool IsGrayscale { get; }

        /// <summary>
        /// Renders <paramref name="tileScene"/> (its viewport is the tile size) centered on the given volume point of
        /// <paramref name="sectionNumber"/> to a new render target the caller disposes; null when cancelled or not possible.
        /// With <paramref name="asyncTextureLoad"/> false the tile textures are loaded before rendering.
        /// </summary>
        Task<RenderTarget2D> RenderSceneToTexture(VikingXNA.Scene tileScene, float centerX, float centerY, int sectionNumber,
            bool showOverlays, bool asyncTextureLoad, CancellationToken token);
    }

    /// <summary>Viking's section viewer as a <see cref="ISegmentationViewport"/>.</summary>
    internal sealed class SectionViewerSegmentationViewport(SectionViewerControl viewer) : ISegmentationViewport
    {
        public double CameraDownsample => viewer.Camera?.Downsample ?? viewer.Downsample;

        public int ViewportWidth => viewer.Width;

        public int ViewportHeight => viewer.Height;

        public Geometry.Vector2 ScreenToWorld(double x, double y) => viewer.ScreenToWorld(x, y);

        public int SectionNumber => viewer.Section?.Number ?? 0;

        public string VolumeName => viewer.Section?.VolumeViewModel?.Name ?? string.Empty;

        /// <summary>
        /// A section the active volume transform has no entry for is drawn with an identity transform, so it is in mosaic
        /// space even while a volume transform is chosen.
        /// </summary>
        public bool InVolumeSpace => viewer.Section?.VolumeViewModel?.HasVolumeTransformFor(SectionNumber) ?? false;

        public string VolumeTransform => InVolumeSpace ? viewer.Section.VolumeViewModel.ActiveVolumeTransform : string.Empty;

        public string SectionTransform => viewer.CurrentTransform ?? string.Empty;

        public string Channel => viewer.CaptureChannelKey;

        public bool IsGrayscale => viewer.CurrentChannelset.Length == 1;

        public Task<RenderTarget2D> RenderSceneToTexture(VikingXNA.Scene tileScene, float centerX, float centerY, int sectionNumber,
            bool showOverlays, bool asyncTextureLoad, CancellationToken token) =>
            viewer.RenderSceneToTexture(tileScene, centerX, centerY, sectionNumber, showOverlays, asyncTextureLoad, token);
    }
}
