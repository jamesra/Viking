using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Viking.SectionCorrectionClient;
using Viking.ViewModels;

#nullable enable

namespace Viking.UI.Controls
{
    /// <summary>
    /// Section-correction shim: a view-only translation of the section on screen, supplied by the correction
    /// service Identity assigned to the open annotation context, and the status-bar item that shows and toggles it.
    /// </summary>
    /// <remarks>
    /// The shim lives in <see cref="VikingXNA.Camera.Shim"/>. Tiles and annotations are drawn through the same
    /// camera, so both move together, while <see cref="VikingXNA.Scene.ScreenToWorld(Geometry.Vector2)"/> maps the
    /// pointer back to true volume coordinates. Annotation saving therefore needs no shim handling.
    /// The fade range and the on/off switch are <see cref="SectionShimContext.Settings"/>, edited in the annotation
    /// preferences.
    /// </remarks>
    public partial class SectionViewerControl
    {
        static readonly System.Drawing.Color CorrectionOnColor = System.Drawing.Color.FromArgb(0, 140, 0);
        static readonly System.Drawing.Color CorrectionOffColor = System.Drawing.Color.FromArgb(200, 0, 0);

        SectionShimService? _correctionShim;
        bool _correctionShimCreated;
        ToolStripLabel? _tsCorrection;

        /// <summary>The shim currently applied to the camera, in nanometres.</summary>
        double _correctionOffsetXNm;
        double _correctionOffsetYNm;

        /// <summary>
        /// Adds the "Corrections" item to <paramref name="statusBar"/>. Called once from the constructor.
        /// </summary>
        /// <param name="statusBar">The viewer's status strip.</param>
        void InitializeCorrectionStatus(StatusStrip statusBar)
        {
            _tsCorrection = new ToolStripLabel { AutoToolTip = false };
            _tsCorrection.Click += (_, _) => ToggleCorrectionShim();
            statusBar.Items.Add(_tsCorrection);
            SectionShimContext.SettingsChanged += OnCorrectionSettingsChanged;
            UpdateCorrectionStatus();
        }

        /// <summary>
        /// Turns the correction shim on or off. Does nothing when the open context has no correction service.
        /// </summary>
        void ToggleCorrectionShim()
        {
            if (_correctionShim is null || !_correctionShim.IsAvailable)
                return;

            SectionShimContext.Settings.Enabled = !SectionShimContext.Settings.Enabled;
            SectionShimContext.NotifySettingsChanged();
        }

        /// <summary>
        /// Re-reads <see cref="SectionShimContext.Settings"/> after the preferences dialog or the status-bar toggle
        /// changed them: re-evaluates the shim for the current view and redraws the status item.
        /// </summary>
        public void RefreshCorrectionShim()
        {
            UpdateCorrectionShim();
            UpdateCorrectionStatus();
        }

        void OnCorrectionSettingsChanged()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
                BeginInvoke(new Action(RefreshCorrectionShim));
            else
                RefreshCorrectionShim();
        }

        /// <summary>
        /// Shows None (grey) when the context has no correction service, otherwise On (green) or Off (red).
        /// </summary>
        void UpdateCorrectionStatus()
        {
            if (_tsCorrection is null)
                return;

            if (_correctionShim is null || !_correctionShim.IsAvailable)
            {
                _tsCorrection.Text = "Corrections: None";
                _tsCorrection.ForeColor = System.Drawing.SystemColors.GrayText;
                _tsCorrection.ToolTipText = "This annotation context has no correction service.";
                return;
            }

            bool on = SectionShimContext.Settings.Enabled;
            _tsCorrection.Text = on ? "Corrections: On" + CorrectionOffsetText() : "Corrections: Off";
            _tsCorrection.ForeColor = on ? CorrectionOnColor : CorrectionOffColor;
            _tsCorrection.ToolTipText = on
                ? "Section corrections are shifting the view. Click to turn them off."
                : "Section corrections are off. Click to turn them on.";
        }

        /// <summary>
        /// " - 2.4x, 16.8y nm" when the offset preference is on, otherwise empty.
        /// </summary>
        string CorrectionOffsetText()
        {
            if (!SectionShimContext.Settings.ShowOffset)
                return string.Empty;

            return string.Format(System.Globalization.CultureInfo.CurrentCulture, " - {0:0.0}x, {1:0.0}y nm",
                _correctionOffsetXNm, _correctionOffsetYNm);
        }

        /// <summary>
        /// Creates the shim service the first time a section is shown for a context that has a correction service.
        /// Does nothing when Identity sent no URL or the gRPC source is unavailable.
        /// </summary>
        void EnsureCorrectionShim()
        {
            if (_correctionShimCreated || _Section is null)
                return;

            ISectionShimSource? source = SectionShimContext.Source;
            string? url = State.CorrectionServiceUrl;
            string? database = State.CorrectionDatabaseName;
            if (source is null || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(database))
                return;

            _correctionShimCreated = true;
            _correctionShim = new SectionShimService(source, ApplyCorrectionShimToCamera, SectionShimContext.Settings,
                message => Trace.WriteLine(message, "CorrectionShim"));
            _correctionShim.Configure(url, database, _Section.VolumeViewModel.DefaultXYScale.Value);
            Trace.WriteLine($"Corrections available: service URL '{url}', database '{database}', IsAvailable={_correctionShim.IsAvailable}.", "CorrectionShim");
            UpdateCorrectionStatus();
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
                downsample,
                AdjacentShimSectionNumbers(_Section.Number));
        }

        /// <summary>How many sections on each side of the one on screen have their correction lattice prefetched.</summary>
        const int ShimPrefetchSections = 3;

        /// <summary>
        /// The section numbers within <see cref="ShimPrefetchSections"/> steps below and above
        /// <paramref name="sectionNumber"/> in the volume's own order (section numbers may have gaps), nearest first and
        /// alternating below/above. Empty when the volume or the section is unknown.
        /// </summary>
        static long[] AdjacentShimSectionNumbers(int sectionNumber)
        {
            SortedList<int, SectionViewModel>? sections = State.volume?.SectionViewModels;
            int index = sections?.IndexOfKey(sectionNumber) ?? -1;
            if (sections is null || index < 0)
                return [];

            var neighbors = new List<long>(2 * ShimPrefetchSections);
            for (int step = 1; step <= ShimPrefetchSections; step++)
            {
                if (index - step >= 0)
                    neighbors.Add(sections.Keys[index - step]);
                if (index + step < sections.Count)
                    neighbors.Add(sections.Keys[index + step]);
            }

            return [.. neighbors];
        }

        /// <summary>How long the camera shim takes to glide to a new value.</summary>
        const int ShimTweenMilliseconds = 200;

        System.Windows.Forms.Timer? _shimTweenTimer;
        readonly Stopwatch _shimTweenClock = new();
        Vector2 _shimTweenFrom;
        Vector2 _shimTweenTo;

        /// <summary>
        /// Glides <see cref="VikingXNA.Camera.Shim"/> from where it is now to <paramref name="target"/> so a late
        /// correction answer slides the image instead of snapping it. A new target mid-glide restarts from the
        /// current position. UI thread only.
        /// </summary>
        void StartShimTween(Vector2 target)
        {
            _shimTweenFrom = Camera.Shim;
            _shimTweenTo = target;
            if (Vector2.DistanceSquared(_shimTweenFrom, target) < 1e-8f)
            {
                _shimTweenTimer?.Stop();
                return;
            }

            if (_shimTweenTimer is null)
            {
                _shimTweenTimer = new System.Windows.Forms.Timer { Interval = 15 };
                _shimTweenTimer.Tick += (_, _) => StepShimTween();
            }

            _shimTweenClock.Restart();
            _shimTweenTimer.Start();
        }

        void StepShimTween()
        {
            if (IsDisposed)
            {
                _shimTweenTimer?.Stop();
                return;
            }

            float t = Math.Min(1f, (float)(_shimTweenClock.ElapsedMilliseconds / (double)ShimTweenMilliseconds));
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            Camera.Shim = Vector2.Lerp(_shimTweenFrom, _shimTweenTo, eased);
            if (t >= 1f)
                _shimTweenTimer?.Stop();

            Invalidate();
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

            StartShimTween(new Vector2((float)dx, (float)dy));

            if (_Section is not null)
            {
                double nmPerPixel = _Section.VolumeViewModel.DefaultXYScale.Value;
                _correctionOffsetXNm = dx * nmPerPixel;
                _correctionOffsetYNm = dy * nmPerPixel;
                if (SectionShimContext.Settings.ShowOffset)
                    UpdateCorrectionStatus();
            }
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
            SectionShimContext.SettingsChanged -= OnCorrectionSettingsChanged;
            _shimTweenTimer?.Dispose();
            _shimTweenTimer = null;
            _correctionShim?.Dispose();
            _correctionShim = null;
        }
    }
}
