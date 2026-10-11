using System;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Viking.SectionCorrectionClient;

#nullable enable

namespace Viking.UI.Controls
{
    /// <summary>
    /// Section-correction shim: a view-only translation of the section on screen, supplied by the correction
    /// service Identity assigned to the open annotation context.
    /// </summary>
    /// <remarks>
    /// The shim lives in <see cref="VikingXNA.Camera.Shim"/>. Tiles and annotations are drawn through the same
    /// camera, so both move together, while <see cref="VikingXNA.Scene.ScreenToWorld(Geometry.Vector2)"/> maps the
    /// pointer back to true volume coordinates. Annotation saving therefore needs no shim handling.
    /// </remarks>
    public partial class SectionViewerControl
    {
        SectionShimService? _correctionShim;
        bool _correctionShimCreated;

        /// <summary>
        /// Creates the shim service the first time a section is shown for a context that has a correction service.
        /// Does nothing when Identity sent no URL or the gRPC source is unavailable.
        /// </summary>
        void EnsureCorrectionShim()
        {
            if (_correctionShimCreated || _Section is null)
                return;

            ISectionShimSource? source = State.SectionShimSource;
            string? url = State.CorrectionServiceUrl;
            string? database = State.CorrectionDatabaseName;
            if (source is null || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(database))
                return;

            _correctionShimCreated = true;
            var settings = new SectionShimSettings();
            _correctionShim = new SectionShimService(source, ApplyCorrectionShimToCamera, settings,
                message => Trace.WriteLine(message, "CorrectionShim"));
            ApplyCorrectionShimSettings();
            _correctionShim.Configure(url, database, _Section.VolumeViewModel.DefaultXYScale.Value);
        }

        /// <summary>
        /// Copies the fade preferences into the running service. Called after the preferences dialog saves.
        /// </summary>
        void ApplyCorrectionShimSettings()
        {
            if (_correctionShim is null)
                return;

            _correctionShim.Settings.FadeStartDownsample = Viking.Properties.Settings.Default.CorrectionShimFadeStartDownsample;
            _correctionShim.Settings.FadeEndDownsample = Viking.Properties.Settings.Default.CorrectionShimFadeEndDownsample;
            UpdateCorrectionShim();
        }

        /// <summary>
        /// Reports the visible scene to the shim service. Cheap; called on every camera, section and transform change.
        /// </summary>
        void UpdateCorrectionShim()
        {
            EnsureCorrectionShim();
            if (_correctionShim is null || Scene is null || _Section is null)
                return;

            VikingXNA.Camera camera = Scene.Camera;
            double downsample = camera.Downsample;
            // LookAt, not EffectiveLookAt: the request describes the view before the shim so it cannot feed back on itself.
            _correctionShim.SceneChanged(
                _Section.VolumeViewModel.ActiveVolumeTransform,
                _Section.Number,
                camera.LookAt.X,
                camera.LookAt.Y,
                Scene.Viewport.Width * downsample / 2.0,
                Scene.Viewport.Height * downsample / 2.0,
                downsample);
        }

        /// <summary>
        /// Receives the faded shim in volume units on whatever thread produced it and moves it to the UI thread.
        /// </summary>
        void ApplyCorrectionShimToCamera(double dx, double dy)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => ApplyCorrectionShimToCamera(dx, dy)));
                }
                catch (InvalidOperationException)
                {
                    // Handle destroyed while a request was in flight.
                }

                return;
            }

            Camera.Shim = new Vector2((float)dx, (float)dy);
            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnSceneChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnSceneChanged(sender, e);

            // Camera.Shim is excluded: applying the shim raises it, and it must not trigger another report.
            if (e.PropertyName == "Camera.LookAt" || e.PropertyName == "Camera.Downsample" || e.PropertyName == "Viewport")
                UpdateCorrectionShim();
        }

        void DisposeCorrectionShim()
        {
            _correctionShim?.Dispose();
            _correctionShim = null;
        }
    }
}
