using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Viking.VolumeModel;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands.Segmentation;
using JotunnSectionVM = Viking.VolumeViewModel.SectionViewModel;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;

namespace Viking.VolumeView
{
    /// <summary>
    /// The active grid cell as a <see cref="ISegmentationViewport"/>, so Viking's segmentation session can capture and
    /// upload mosaic tiles from Jotunn's view, and as the view auto-polygonize proposes outlines in.
    /// </summary>
    public partial class SectionSceneHost : ISegmentationViewport, IAutoPolygonizeHost
    {
        /// <summary>A short message for the status bar (auto-polygonize reports a failed save this way).</summary>
        public event EventHandler<string> TransientStatus;

        bool IAutoPolygonizeHost.HasSection => Volume != null && Volume.Sections.ContainsKey(SectionNumber);

        bool IAutoPolygonizeHost.HasCamera => Scene != null;

        Viking.VolumeModel.IVolumeToSectionTransform IAutoPolygonizeHost.ActiveSectionToVolumeTransform =>
            WebAnnotation.AnnotationBootstrap.Transforms?.GetSectionToVolumeTransform(SectionNumber);

        VikingXNA.Scene _activeCellScene;

        /// <summary>
        /// The active cell's view (its viewport, the shared camera), not the whole grid: auto-polygonize sizes its work
        /// and picks circles from the scene's visible bounds. One scene is kept and its viewport updated, because each
        /// scene subscribes to the camera.
        /// </summary>
        VikingXNA.Scene IAutoPolygonizeHost.Scene => OnUiThread(() =>
        {
            if (Scene == null)
                return null;

            ApplyActiveCellViewport();
            Viewport cell = Scene.Viewport;
            if (_activeCellScene == null || !ReferenceEquals(_activeCellScene.Camera, Scene.Camera))
            {
                _activeCellScene?.Dispose();
                _activeCellScene = new VikingXNA.Scene(cell, Scene.Camera);
            }
            else
            {
                _activeCellScene.Viewport = cell;
            }

            return _activeCellScene;
        });

        double IAutoPolygonizeHost.Downsample => Downsample;

        GraphicsDevice IAutoPolygonizeHost.Device => Device;

        /// <summary>Called from store and cache events too, which can arrive on worker threads; WPF needs the UI thread.</summary>
        void IAutoPolygonizeHost.Invalidate()
        {
            if (Dispatcher.CheckAccess())
                Invalidate();
            else
                Dispatcher.BeginInvoke(new Action(Invalidate));
        }

        void IAutoPolygonizeHost.ShowTransientStatus(string message)
        {
            if (Dispatcher.CheckAccess())
                TransientStatus?.Invoke(this, message);
            else
                Dispatcher.BeginInvoke(new Action(() => TransientStatus?.Invoke(this, message)));
        }

        bool IAutoPolygonizeHost.IsIdle => AnnotationTools?.IsIdle ?? true;

        bool IAutoPolygonizeHost.IsSegmenting => AnnotationTools?.IsSegmenting ?? false;

        bool IAutoPolygonizeHost.InvokeRequired => !Dispatcher.CheckAccess();

        void IAutoPolygonizeHost.BeginInvoke(Action action) => Dispatcher.BeginInvoke(action);

        Viking.Services.Grpc.IGrpcChannelManager IAutoPolygonizeHost.SegmentationChannels => AnnotationTools?.SegmentationChannels;

        void IAutoPolygonizeHost.StartSegmentation(ProposalSegmentationRequest request) =>
            AnnotationTools?.StartProposalSegmentation(this, request);

        /// <summary>Longest a capture waits for its tile textures before rendering what has loaded.</summary>
        static readonly TimeSpan CaptureTextureWait = TimeSpan.FromSeconds(20);

        double ISegmentationViewport.CameraDownsample => Scene?.Camera.Downsample ?? 1;

        int ISegmentationViewport.ViewportWidth => OnUiThread(() =>
        {
            ApplyActiveCellViewport();
            return Scene?.Viewport.Width ?? 1;
        });

        int ISegmentationViewport.ViewportHeight => OnUiThread(() =>
        {
            ApplyActiveCellViewport();
            return Scene?.Viewport.Height ?? 1;
        });

        Geometry.Vector2 ISegmentationViewport.ScreenToWorld(double x, double y) => OnUiThread(() =>
        {
            ApplyActiveCellViewport();
            return ScreenToWorld(new Geometry.Vector2(x, y));
        });

        /// <summary>
        /// Reading the active cell sets the scene's viewport, which the render loop also uses, so segmentation and
        /// auto-polygonize work running on worker threads reads it on the UI thread.
        /// </summary>
        T OnUiThread<T>(Func<T> read) => Dispatcher.CheckAccess() ? read() : Dispatcher.Invoke(read);

        int ISegmentationViewport.SectionNumber => SectionNumber;

        string ISegmentationViewport.VolumeName => Volume?.Name ?? string.Empty;

        string ISegmentationViewport.VolumeTransform =>
            ActiveVolumeVm()?.ActiveVolumeTransform ?? Volume?.DefaultVolumeTransform ?? string.Empty;

        string ISegmentationViewport.SectionTransform => SectionTransformFor(SectionNumber);

        string ISegmentationViewport.Channel => ChannelFor(SectionNumber);

        /// <summary>Jotunn draws one channel per cell.</summary>
        bool ISegmentationViewport.IsGrayscale => true;

        VolumeVM ActiveVolumeVm() => Grid?.DataContext as VolumeVM ?? DataContext as VolumeVM;

        JotunnSectionVM SectionVmFor(int sectionNumber)
        {
            VolumeVM volume = ActiveVolumeVm();
            return volume != null && volume.SectionViewModels.ContainsKey(sectionNumber) ? volume.SectionViewModels[sectionNumber] : null;
        }

        string SectionTransformFor(int sectionNumber)
        {
            Section section = Volume != null && Volume.Sections.TryGetValue(sectionNumber, out Section s) ? s : null;
            return SectionVmFor(sectionNumber)?.SelectedSectionTransform ?? section?.DefaultPyramidTransform ?? string.Empty;
        }

        string ChannelFor(int sectionNumber)
        {
            Section section = Volume != null && Volume.Sections.TryGetValue(sectionNumber, out Section s) ? s : null;
            return SectionVmFor(sectionNumber)?.SelectedChannel ?? section?.DefaultChannel ?? string.Empty;
        }

        /// <summary>
        /// Draws the section's tiles for <paramref name="tileScene"/> into a new render target, as Viking's viewer does
        /// for segmentation captures. Unless <paramref name="asyncTextureLoad"/> is set, it draws again every 100 ms until
        /// every tile's texture has loaded (or <see cref="CaptureTextureWait"/> passes). Runs on the UI thread, which owns
        /// the device. Annotations are never drawn into a capture.
        /// </summary>
        public async Task<RenderTarget2D> RenderSceneToTexture(VikingXNA.Scene tileScene, float centerX, float centerY, int sectionNumber,
            bool showOverlays, bool asyncTextureLoad, CancellationToken token)
        {
            if (!Dispatcher.CheckAccess())
            {
                return await Dispatcher.InvokeAsync(() =>
                    RenderSceneToTexture(tileScene, centerX, centerY, sectionNumber, showOverlays, asyncTextureLoad, token)).Task.Unwrap()
                    .ConfigureAwait(false);
            }

            tileScene.Camera.LookAt = new Microsoft.Xna.Framework.Vector2(centerX, centerY);
            GraphicsDevice device = Device;
            if (device == null || Volume == null || !Volume.Sections.TryGetValue(sectionNumber, out Section section))
                return null;

            _renderer.EnsureMappingInitialized(section, token);
            RenderTarget2D target = new(device, tileScene.Viewport.Width, tileScene.Viewport.Height, false,
                SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);

            Stopwatch waited = Stopwatch.StartNew();
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    bool complete = DrawTilesInto(target, device, tileScene, section, token);
                    if (complete || asyncTextureLoad || waited.Elapsed > CaptureTextureWait)
                    {
                        if (!complete)
                            Trace.WriteLine($"Segmentation capture of section {sectionNumber} used partly loaded tiles");
                        return target;
                    }

                    await Task.Delay(100, token).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                target.Dispose();
                return null;
            }
            catch
            {
                target.Dispose();
                throw;
            }
        }

        /// <summary>One synchronous tile pass into <paramref name="target"/>; restores the device and renderer state after.</summary>
        bool DrawTilesInto(RenderTarget2D target, GraphicsDevice device, VikingXNA.Scene tileScene, Section section, CancellationToken token)
        {
            RenderTargetBinding[] savedTargets = device.GetRenderTargets();
            Viewport savedViewport = device.Viewport;
            bool savedAsync = _renderer.AsynchTextureLoad;
            string savedVolumeTransform = _renderer.VolumeTransformName;
            string savedSectionTransform = _renderer.SectionTransformName;
            try
            {
                device.SetRenderTarget(target);
                device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Microsoft.Xna.Framework.Color.Black, 1f, 0);
                // Full-resolution tiles only, as Viking's capture does with synchronous texture loads.
                _renderer.AsynchTextureLoad = false;
                _renderer.VolumeTransformName = ActiveVolumeVm()?.ActiveVolumeTransform ?? string.Empty;
                _renderer.SectionTransformName = SectionTransformFor(section.Number);
                int drawn = _renderer.DrawTiles(device, tileScene, section, ChannelFor(section.Number), token,
                    loadFullResolution: true, queueTextureLoads: true);
                return drawn >= 0 && _renderer.LastDrawTilesComplete;
            }
            finally
            {
                device.SetRenderTargets(savedTargets);
                device.Viewport = savedViewport;
                _renderer.AsynchTextureLoad = savedAsync;
                _renderer.VolumeTransformName = savedVolumeTransform;
                _renderer.SectionTransformName = savedSectionTransform;
            }
        }
    }
}
