using System;
using System.Collections.Generic;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI.Commands;
using Viking.UI.Controls;
using Viking.VolumeModel;
using WebAnnotation.UI.Commands;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Interactive segmentation seeded from an auto-polygonize proposal ("Add points"): the prompts to start from, the
    /// location and structure it is for, and what to do with an accepted outline or an abandoned edit.
    /// </summary>
    internal sealed record ProposalSegmentationRequest(
        IReadOnlyList<Vector2> Foreground,
        IReadOnlyList<SegmentationSeed> StartingBoxes,
        long StructureTypeId,
        long LocationId,
        long? StructureId,
        Action<Polygon> OnAccepted,
        Action OnAbandoned);

    /// <summary>
    /// The view <see cref="AutoCirclePolygonizeController"/> proposes outlines in: the section and camera it reads, the
    /// device its overlays draw with, and the view's command state, which decides when idle work may run. Viking's
    /// section viewer and Jotunn's scene host both provide it.
    /// </summary>
    internal interface IAutoPolygonizeHost : ISegmentationViewport
    {
        /// <summary>False while no section is shown.</summary>
        bool HasSection { get; }

        /// <summary>False while the view has no camera yet.</summary>
        bool HasCamera { get; }

        IVolumeToSectionTransform ActiveSectionToVolumeTransform { get; }

        VikingXNA.Scene Scene { get; }

        double Downsample { get; }

        GraphicsDevice Device { get; }

        void Invalidate();

        void ShowTransientStatus(string message);

        /// <summary>True when the user is not in the middle of a command, so idle proposals may run and show.</summary>
        bool IsIdle { get; }

        /// <summary>True while interactive segmentation is active; proposal work stays out of its way.</summary>
        bool IsSegmenting { get; }

        bool InvokeRequired { get; }

        void BeginInvoke(Action action);

        /// <summary>Opens interactive segmentation seeded from a proposal.</summary>
        void StartSegmentation(ProposalSegmentationRequest request);
    }

    /// <summary>Viking's section viewer as an <see cref="IAutoPolygonizeHost"/>.</summary>
    internal sealed class SectionViewerAutoPolygonizeHost(SectionViewerControl viewer) : IAutoPolygonizeHost
    {
        readonly SectionViewerSegmentationViewport _viewport = new(viewer);

        public double CameraDownsample => viewer.Camera?.Downsample ?? viewer.Downsample;

        public int ViewportWidth => _viewport.ViewportWidth;

        public int ViewportHeight => _viewport.ViewportHeight;

        public Vector2 ScreenToWorld(double x, double y) => _viewport.ScreenToWorld(x, y);

        public int SectionNumber => viewer.Section?.Number ?? 0;

        public string VolumeName => _viewport.VolumeName;

        public string VolumeTransform => _viewport.VolumeTransform;

        public string SectionTransform => _viewport.SectionTransform;

        public string Channel => _viewport.Channel;

        public bool IsGrayscale => _viewport.IsGrayscale;

        public System.Threading.Tasks.Task<RenderTarget2D> RenderSceneToTexture(VikingXNA.Scene tileScene, float centerX, float centerY,
            int sectionNumber, bool showOverlays, bool asyncTextureLoad, System.Threading.CancellationToken token) =>
            _viewport.RenderSceneToTexture(tileScene, centerX, centerY, sectionNumber, showOverlays, asyncTextureLoad, token);

        public bool HasSection => viewer.Section is not null;

        public bool HasCamera => viewer.Camera is not null;

        public IVolumeToSectionTransform ActiveSectionToVolumeTransform => viewer.Section?.ActiveSectionToVolumeTransform;

        public VikingXNA.Scene Scene => viewer.Scene;

        public double Downsample => viewer.Downsample;

        public GraphicsDevice Device => viewer.Device;

        public void Invalidate() => viewer.Invalidate();

        public void ShowTransientStatus(string message) => viewer.ShowTransientStatus(message);

        public bool IsIdle =>
            viewer.CurrentCommand is null ||
            (viewer.CurrentCommand.GetType() == typeof(DefaultCommand) && viewer.CommandQueue.QueueDepth == 0);

        public bool IsSegmenting => viewer.CurrentCommand is SegmentationCommand;

        public bool InvokeRequired => viewer.InvokeRequired;

        public void BeginInvoke(Action action) => viewer.BeginInvoke(action);

        public void StartSegmentation(ProposalSegmentationRequest request)
        {
            Viking.Services.Grpc.IGrpcChannelManager channelManager =
                Viking.DependencyInjection.ServiceLocator.GetRequiredService<Viking.Services.Grpc.IGrpcChannelManager>();
            viewer.CurrentCommand = new SegmentationCommand(
                viewer,
                request.Foreground,
                Array.Empty<Vector2>(),
                polygon => request.OnAccepted(polygon),
                channelManager,
                request.StructureTypeId,
                request.LocationId,
                request.StructureId,
                startingBoxes: request.StartingBoxes,
                onAbandoned: request.OnAbandoned);
        }
    }
}
