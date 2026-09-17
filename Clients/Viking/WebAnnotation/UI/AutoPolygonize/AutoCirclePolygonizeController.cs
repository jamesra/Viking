using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.UI;
using Viking.UI.Commands;
using Viking.UI.Controls;
using Viking.VolumeModel;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Idle-driven SAM2 proposals for circles in the current view. Owned by
    /// <see cref="AnnotationOverlay"/>; not a Viking command.
    /// Capture uses a two-phase settle so a UI hitch cannot pass the 500ms idle
    /// timer. After upload, camera moves cancel only the upload phase so already
    /// collected points still segment. Section changes and the interactive Segment
    /// command cancel both phases.
    /// </summary>
    internal sealed class AutoCirclePolygonizeController
    {
        /// <summary>Wall-clock quiet time before arming a confirm snapshot. The timer keeps running during UI hitches.</summary>
        private const int IdleDebounceMs = 500;

        /// <summary>Second delay after a snapshot. Capture starts only if bounds and downsample still match.</summary>
        private const int ConfirmDelayMs = 1000;

        private const int RegionPollMs = 150;
        private const int RegionWaitTimeoutMs = 10000;
        private static long profileBatchSequence;

        private readonly SectionViewerControl parent;
        private readonly Action requestAnnotationLoad;
        private readonly AutoPolygonizeCache cache = new();
        private readonly Dictionary<long, AutoPolygonizeProposal> proposals = [];
        private readonly object proposalLock = new();

        private bool enabled;
        private System.Timers.Timer? idleTimer;
        private System.Timers.Timer? confirmTimer;

        /// <summary>Viewport captured when the idle timer fired; compared again after <see cref="ConfirmDelayMs"/>.</summary>
        private GridRectangle armedBounds;
        private double armedDownsample;
        private DateTime armedAtUtc;

        /// <summary>Updated on the UI thread. A confirm callback aborts if this is newer than <see cref="armedAtUtc"/>.</summary>
        private DateTime lastCameraChangeUtc;

        /// <summary>Cancelled on camera/section change. Does not cancel in-flight SegmentImage work.</summary>
        private CancellationTokenSource? uploadCts;

        /// <summary>Cancelled on section change, dispose, or when SegmentationCommand is active.</summary>
        private CancellationTokenSource processCts = new();
        private SegmentationViewportSession? uploadSession;
        private readonly List<ProcessBatch> processBatches = [];
        private readonly object lifecycleLock = new();
        private GridRectangle lastViewBounds;
        private AutoPolygonizeProposal? hoveredProposal;

        /// <summary>
        /// One uploaded viewport plus the per-response polygonize tasks that still
        /// own that server image. Survives camera moves after upload.
        /// </summary>
        private sealed class ProcessBatch(SegmentationViewportSession session)
        {
            public SegmentationViewportSession Session { get; } = session;
            public List<Task> ResponseTasks { get; } = [];
        }

        public AutoCirclePolygonizeController(SectionViewerControl parent, Action requestAnnotationLoad)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.requestAnnotationLoad = requestAnnotationLoad ?? throw new ArgumentNullException(nameof(requestAnnotationLoad));
        }

        public bool IsEnabled => enabled;

        /// <summary>
        /// True when no non-default command is running. Hidden during Segment and
        /// queued commands so proposal overlays do not fight those tools.
        /// </summary>
        private bool ShouldShowProposals =>
            parent.CurrentCommand is null ||
            (parent.CurrentCommand.GetType() == typeof(DefaultCommand) &&
             parent.CommandQueue.QueueDepth == 0);

        /// <summary>
        /// Starts or stops idle/confirm timers and location-store subscription.
        /// Called from the Auto Polygonize Circles setting.
        /// </summary>
        public void SetEnabled(bool value)
        {
            if (enabled == value)
                return;

            enabled = value;
            if (enabled)
            {
                Store.Locations.OnCollectionChanged += OnLocationsChanged;
                idleTimer = new System.Timers.Timer(IdleDebounceMs)
                {
                    AutoReset = false
                };
                idleTimer.Elapsed += OnIdleElapsed;
                confirmTimer = new System.Timers.Timer(ConfirmDelayMs)
                {
                    AutoReset = false
                };
                confirmTimer.Elapsed += OnConfirmElapsed;
                lastViewBounds = GetCurrentViewportBounds();
                lastCameraChangeUtc = DateTime.UtcNow;
                RestartIdleTimer();
            }
            else
            {
                DisableAndClear();
            }
        }

        /// <summary>
        /// Stops the idle timer and any in-flight batch without invalidating a disposing parent.
        /// </summary>
        public void Stop()
        {
            enabled = false;
            Store.Locations.OnCollectionChanged -= OnLocationsChanged;
            CancelUploadPhase();
            CancelProcessPhase();
            StopSettleTimers();
            idleTimer?.Dispose();
            idleTimer = null;
            confirmTimer?.Dispose();
            confirmTimer = null;
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in proposals.Values)
                    proposal.DisposeMaskOverlay();

                proposals.Clear();
            }
            hoveredProposal = null;
            cache.Clear();
        }

        /// <summary>
        /// Restarts settle when the view actually moved. Aborts capture/encode only;
        /// an already-uploaded batch keeps segmenting.
        /// </summary>
        public void OnCameraChanged()
        {
            if (!enabled)
                return;

            GridRectangle current = GetCurrentViewportBounds();
            if (SegmentationViewportSession.AreViewportBoundsSimilar(lastViewBounds, current))
                return;

            lastViewBounds = current;
            lastCameraChangeUtc = DateTime.UtcNow;
            CancelUploadPhase();
            CancelConfirmTimer();
            RestartIdleTimer();
        }

        /// <summary>
        /// Drops upload and process work. Proposals stay in the dictionary; Draw
        /// filters them by the new section number.
        /// </summary>
        public void OnSectionChanged()
        {
            if (!enabled)
                return;

            lastCameraChangeUtc = DateTime.UtcNow;
            CancelUploadPhase();
            CancelProcessPhase();
            CancelConfirmTimer();
            RestartIdleTimer();
        }

        /// <summary>
        /// Preference toggle for the debug SAM2 mask overlay. Turning it off
        /// disposes GPU textures; turning it on does not backfill existing proposals.
        /// </summary>
        public void OnOverlayMasksChanged(bool enabled)
        {
            if (!enabled)
                DisposeAllMaskOverlays();

            parent.Invalidate();
        }

        /// <summary>Draws proposals for the current section when <see cref="ShouldShowProposals"/> is true.</summary>
        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            if (!enabled || !ShouldShowProposals || parent.Section is null)
                return;

            int sectionNumber = parent.Section.Number;
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in proposals.Values)
                {
                    if (proposal.SectionNumber == sectionNumber)
                        proposal.Draw(graphicsDevice, scene);
                }
            }
        }

        /// <summary>
        /// Nearest proposal ring within the downsample-scaled hit radius.
        /// </summary>
        /// <returns>False when overlays are hidden or nothing is in range.</returns>
        public bool TryHit(GridVector2 worldPosition, out AutoPolygonizeProposal proposal, out double distance)
        {
            proposal = null;
            distance = double.MaxValue;
            if (!enabled || !ShouldShowProposals || parent.Section is null || parent.Camera is null)
                return false;

            double threshold = parent.Camera.Downsample * AutoPolygonizeSelection.HitTestPixels;
            int sectionNumber = parent.Section.Number;

            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal candidate in proposals.Values)
                {
                    if (candidate.SectionNumber != sectionNumber)
                        continue;

                    if (!candidate.TryHit(worldPosition, threshold, out double candidateDistance))
                        continue;

                    if (candidateDistance < distance)
                    {
                        distance = candidateDistance;
                        proposal = candidate;
                    }
                }
            }

            return proposal is not null;
        }

        /// <summary>Highlights the proposal under the cursor and invalidates when the hit changes.</summary>
        public void UpdateHover(GridVector2 worldPosition)
        {
            TryHit(worldPosition, out AutoPolygonizeProposal nextProposal, out _);
            if (ReferenceEquals(hoveredProposal, nextProposal))
                return;

            if (hoveredProposal is not null)
                hoveredProposal.IsHighlighted = false;

            hoveredProposal = nextProposal;
            if (hoveredProposal is not null)
                hoveredProposal.IsHighlighted = true;

            parent.Invalidate();
        }

        /// <summary>Writes the proposal polygon onto the circle location and removes it from the cache.</summary>
        public void Accept(AutoPolygonizeProposal proposal)
        {
            if (proposal is null)
                return;

            LocationObj location = Store.Locations.GetObjectByID(proposal.LocationId, false);
            if (location is null)
            {
                RemoveProposal(proposal.LocationId);
                return;
            }

            if (LocationShapeUpdate.ApplyVolumePolygon(location, proposal.Polygon, parent))
            {
                cache.Remove(proposal.LocationId);
                RemoveProposal(proposal.LocationId);
                parent.Invalidate();
            }
        }

        /// <summary>Hides the proposal until that location's LastModified changes.</summary>
        public void Dismiss(AutoPolygonizeProposal proposal)
        {
            if (proposal is null)
                return;

            cache.Dismiss(proposal.LocationId, proposal.LastModified);
            RemoveProposal(proposal.LocationId);
            parent.Invalidate();
        }

        /// <summary>SetEnabled(false) path: Stop plus a redraw so leftover overlays disappear.</summary>
        private void DisableAndClear()
        {
            Stop();
            parent.Invalidate();
        }

        /// <summary>Cancels an armed confirm and starts a fresh 500ms idle. Used after any real camera/section change.</summary>
        private void RestartIdleTimer()
        {
            CancelConfirmTimer();
            if (idleTimer is null)
                return;

            idleTimer.Stop();
            idleTimer.Start();
        }

        /// <summary>Stops the confirm timer without disposing it so a later idle can re-arm it.</summary>
        private void CancelConfirmTimer()
        {
            confirmTimer?.Stop();
        }

        /// <summary>Stops both settle timers without disposing; Stop() disposes afterward.</summary>
        private void StopSettleTimers()
        {
            idleTimer?.Stop();
            confirmTimer?.Stop();
        }

        /// <summary>
        /// System.Timers callback. Marshals to the UI thread to snapshot the view;
        /// does not start capture here because the timer can fire during a hitch.
        /// </summary>
        private void OnIdleElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!enabled)
                return;

            Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(ArmConfirmIfStillQuiet));
        }

        /// <summary>Confirm-timer callback. Marshals to the UI thread to compare the armed snapshot to the live view.</summary>
        private void OnConfirmElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!enabled)
                return;

            Viking.UI.State.MainThreadDispatcher.BeginInvoke(new Action(RunBatchIfViewStillMatches));
        }

        /// <summary>
        /// Records live bounds/downsample, then starts the 1s confirm. If a camera
        /// event was processed after this method was queued, restarts idle instead.
        /// </summary>
        private void ArmConfirmIfStillQuiet()
        {
            if (!enabled)
                return;

            armedBounds = GetCurrentViewportBounds();
            armedDownsample = GetCurrentDownsample();
            armedAtUtc = DateTime.UtcNow;
            if (lastCameraChangeUtc > armedAtUtc)
            {
                RestartIdleTimer();
                return;
            }

            if (confirmTimer is null)
                return;

            confirmTimer.Stop();
            confirmTimer.Start();
        }

        /// <summary>Starts a batch only when the confirm snapshot still matches the live camera.</summary>
        private void RunBatchIfViewStillMatches()
        {
            if (!enabled)
                return;

            if (lastCameraChangeUtc > armedAtUtc || !IsArmedViewStillCurrent())
            {
                RestartIdleTimer();
                return;
            }

            _ = RunBatchAsync();
        }

        /// <summary>True when live bounds and downsample are within 1% of the armed snapshot.</summary>
        private bool IsArmedViewStillCurrent()
        {
            return SegmentationViewportSession.AreViewportBoundsSimilar(armedBounds, GetCurrentViewportBounds()) &&
                   AreDownsamplesSimilar(armedDownsample, GetCurrentDownsample());
        }

        /// <summary>1% relative compare so float camera downsample noise does not look like a zoom.</summary>
        private static bool AreDownsamplesSimilar(double a, double b)
        {
            double scale = Math.Max(Math.Max(a, b), 1.0);
            return Math.Abs(a - b) <= scale * 0.01;
        }

        private double GetCurrentDownsample() => parent.Camera?.Downsample ?? 0;

        /// <summary>
        /// One capture/upload plus sequential SegmentImage calls. GPU capture stays
        /// on the session UI path; polygonize runs on a task per response. A camera
        /// move after upload does not cancel the process token.
        /// </summary>
        private async Task RunBatchAsync()
        {
            if (!enabled || !Global.IsSegmentationServiceAvailable || parent.Scene is null || parent.Section is null)
                return;

            if (parent.CurrentCommand is SegmentationCommand)
            {
                CancelUploadPhase();
                CancelProcessPhase();
                return;
            }

            CancelUploadPhase();
            CancellationTokenSource nextUploadCts = new();
            lock (lifecycleLock)
            {
                uploadCts = nextUploadCts;
            }

            CancellationToken uploadToken = nextUploadCts.Token;
            long batchId = Interlocked.Increment(ref profileBatchSequence);
            Stopwatch batchTimer = Stopwatch.StartNew();
            SegmentationViewportSession? localUploadSession = null;
            ProcessBatch? processBatch = null;

            try
            {
                Stopwatch stepTimer = Stopwatch.StartNew();
                requestAnnotationLoad();

                if (!await WaitForAnnotationsLoadedAsync(uploadToken).ConfigureAwait(false))
                    return;

                long annotationLoadMs = stepTimer.ElapsedMilliseconds;
                if (uploadToken.IsCancellationRequested || !enabled || parent.CurrentCommand is SegmentationCommand)
                    return;

                stepTimer.Restart();
                localUploadSession = new SegmentationViewportSession(parent);
                if (!localUploadSession.TryInitializeClient())
                    return;

                lock (lifecycleLock)
                {
                    uploadSession = localUploadSession;
                }

                localUploadSession.ViewportBounds = localUploadSession.GetCurrentViewportBounds();
                GridRectangle viewBounds = localUploadSession.ViewportBounds;
                GridRectangle inset = AutoPolygonizeSelection.InsetBounds(viewBounds);
                List<LocationObj> candidates = CollectEligibleCircles(viewBounds, inset);
                if (candidates.Count == 0)
                    return;

                long candidateCollectionMs = stepTimer.ElapsedMilliseconds;
                IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
                int sectionNumber = parent.Section.Number;
                double downsample = parent.Camera.Downsample;
                double simplifyTolerance = Global.PenSimplifyThreshold * parent.Downsample;

                stepTimer.Restart();
                if (!await localUploadSession.UploadCurrentImageAsync(uploadToken).ConfigureAwait(false))
                    return;

                long uploadMs = stepTimer.ElapsedMilliseconds;
                Debug.WriteLine(
                    $"[SegmentationProfile] Auto batch={batchId} initialized " +
                    $"annotations={annotationLoadMs}ms candidates={candidateCollectionMs}ms " +
                    $"upload={uploadMs}ms candidateCount={candidates.Count} total={batchTimer.ElapsedMilliseconds}ms");

                processBatch = new ProcessBatch(localUploadSession);
                lock (lifecycleLock)
                {
                    if (ReferenceEquals(uploadSession, localUploadSession))
                        uploadSession = null;
                    processBatches.Add(processBatch);
                }

                CancellationToken processToken;
                lock (lifecycleLock)
                {
                    processToken = processCts.Token;
                }

                foreach (LocationObj circle in candidates)
                {
                    if (processToken.IsCancellationRequested || !enabled)
                        break;

                    if (parent.CurrentCommand is SegmentationCommand)
                    {
                        CancelProcessPhase();
                        break;
                    }

                    if (!cache.ShouldProcess(circle.ID, circle.LastModified, circle.TypeCode))
                        continue;

                    Stopwatch proposalTimer = Stopwatch.StartNew();
                    IReadOnlyList<GridVector2> foreground = CircleSegmentationPrompts.ToVolumePoints(
                        CircleSegmentationPrompts.CreateMosaicForegroundPoints(new GridCircle(circle.Position, circle.Radius)),
                        transform);

                    if (foreground.Count == 0)
                        continue;

                    IReadOnlyList<GridVector2> background = CircleSegmentationPrompts.CreateBackgroundVolumePoints(
                        CollectVisibleLocationObjs(viewBounds).Where(loc => loc.ID != circle.ID),
                        transform);
                    long promptMs = proposalTimer.ElapsedMilliseconds;

                    Stopwatch segmentTimer = Stopwatch.StartNew();
                    var response = await localUploadSession.SegmentAsync(foreground, background, processToken).ConfigureAwait(false);
                    long segmentMs = segmentTimer.ElapsedMilliseconds;
                    if (response is null)
                        continue;

                    try
                    {
                        Task responseTask = Task.Run(() => ProcessResponse(
                            processBatch.Session,
                            circle,
                            sectionNumber,
                            downsample,
                            simplifyTolerance,
                            foreground.Count,
                            background,
                            response,
                            promptMs,
                            segmentMs,
                            proposalTimer,
                            batchTimer,
                            batchId,
                            processToken), processToken);
                        processBatch.ResponseTasks.Add(responseTask);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                if (processBatch.ResponseTasks.Count > 0)
                    await Task.WhenAll(processBatch.ResponseTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto polygonize batch failed: {ex.Message}");
            }
            finally
            {
                await FinishUploadOrProcessBatchAsync(localUploadSession, processBatch).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Thread-pool polygonize/simplify. Drops the result without caching if the
        /// live view no longer matches the captured bounds, so a hitch-pan can retry.
        /// GPU mask textures are created later on the UI thread in <see cref="PublishProposal"/>.
        /// </summary>
        private void ProcessResponse(
            SegmentationViewportSession session,
            LocationObj circle,
            int sectionNumber,
            double downsample,
            double simplifyTolerance,
            int foregroundCount,
            IReadOnlyList<GridVector2> background,
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentationResponse response,
            long promptMs,
            long segmentMs,
            Stopwatch proposalTimer,
            Stopwatch batchTimer,
            long batchId,
            CancellationToken processToken)
        {
            if (processToken.IsCancellationRequested || !enabled)
                return;

            GridRectangle liveBounds = session.GetLiveViewportBoundsAsync().GetAwaiter().GetResult();
            if (parent.Section is null ||
                parent.Section.Number != sectionNumber ||
                !SegmentationViewportSession.ShouldUploadEncodedCapture(session.ViewportBounds, liveBounds))
            {
                Debug.WriteLine(
                    $"[SegmentationProfile] Auto batch={batchId} location={circle.ID} dropped: view moved before publish");
                return;
            }

            Stopwatch polygonTimer = Stopwatch.StartNew();
            IReadOnlyList<GridPolygon> polygons = session.CreatePolygonsFromResponse(
                response,
                preserveHolesContainingWorldPoints: background);
            GridPolygon polygon = polygons.FirstOrDefault();
            long polygonMs = polygonTimer.ElapsedMilliseconds;
            if (polygon is null)
                return;

            AutoPolygonizeMaskOverlay? maskOverlay = null;
            if (Global.AnnotationSettings.AutoPolygonizeOverlayMasks)
                maskOverlay = AutoPolygonizeMaskOverlay.TryCreate(session, response);

            Stopwatch renderPreparationTimer = Stopwatch.StartNew();
            int verticesBeforeSimplify = polygon.TotalUniqueVerticies;
            polygon = AutoPolygonizeSelection.SimplifyProposal(polygon, simplifyTolerance);
            AutoPolygonizeProposal proposal = new(
                this,
                circle.ID,
                sectionNumber,
                circle.LastModified,
                circle.Radius,
                polygon,
                AutoPolygonizeProposal.CreateRingViews(
                    polygon,
                    AutoPolygonizeProposal.ColorForLocation(circle.ID),
                    circle.Radius,
                    downsample),
                maskOverlay);
            long renderPreparationMs = renderPreparationTimer.ElapsedMilliseconds;

            cache.RememberProposal(circle.ID, circle.LastModified, circle.TypeCode);
            PublishProposal(proposal);
            Debug.WriteLine(
                $"[SegmentationProfile] Auto batch={batchId} location={circle.ID} ready-to-draw " +
                $"prompts={promptMs}ms segmentRpc={segmentMs}ms polygonize={polygonMs}ms " +
                $"simplifyAndViews={renderPreparationMs}ms vertices={verticesBeforeSimplify}->{polygon.TotalUniqueVerticies} " +
                $"proposal={proposalTimer.ElapsedMilliseconds}ms " +
                $"batchElapsed={batchTimer.ElapsedMilliseconds}ms foreground={foregroundCount} background={background.Count}");
        }

        /// <summary>
        /// UI-thread insert into <see cref="proposals"/>. Creates the optional mask
        /// overlay here because Texture2D must be allocated on the graphics thread.
        /// </summary>
        private void PublishProposal(AutoPolygonizeProposal proposal)
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null)
                return;

            dispatcher.BeginInvoke(new Action(() =>
            {
                if (!enabled)
                {
                    proposal.DisposeMaskOverlay();
                    return;
                }

                lock (proposalLock)
                {
                    if (proposals.TryGetValue(proposal.LocationId, out AutoPolygonizeProposal existing))
                        existing.DisposeMaskOverlay();

                    if (Global.AnnotationSettings.AutoPolygonizeOverlayMasks)
                        proposal.AttachMaskOverlay(parent.Device);

                    proposals[proposal.LocationId] = proposal;
                }

                parent.Invalidate();
            }));
        }

        /// <summary>
        /// Polls region queries so circles exist before candidate collection.
        /// </summary>
        /// <returns>False if cancelled; true if queries completed or the timeout expired.</returns>
        private async Task<bool> WaitForAnnotationsLoadedAsync(CancellationToken token)
        {
            if (parent.Scene is null || parent.Section is null)
                return false;

            SectionAnnotationsView sectionView = AnnotationOverlay.GetOrCreateAnnotationsForSection(parent.Section.Number);
            GridRectangle? mosaicBounds = parent.Scene.VisibleWorldBounds.ApproximateVisibleMosaicBounds(sectionView.mapper);
            Stopwatch wait = Stopwatch.StartNew();

            while (!token.IsCancellationRequested && wait.ElapsedMilliseconds < RegionWaitTimeoutMs)
            {
                if (Store.LocationsByRegion.AreRegionQueriesComplete(
                    mosaicBounds,
                    parent.Scene.ScreenPixelSizeInVolume,
                    parent.Section.Number))
                {
                    return true;
                }

                try
                {
                    await Task.Delay(RegionPollMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }

            return !token.IsCancellationRequested;
        }

        /// <summary>
        /// Circles inside the inset, large enough on screen, and not already
        /// proposed or dismissed for the current LastModified.
        /// </summary>
        private List<LocationObj> CollectEligibleCircles(GridRectangle viewBounds, GridRectangle inset)
        {
            List<LocationObj> eligible = [];
            foreach (LocationObj loc in CollectVisibleLocationObjs(viewBounds))
            {
                if (!cache.ShouldProcess(loc.ID, loc.LastModified, loc.TypeCode))
                    continue;

                if (!AutoPolygonizeSelection.IsEligibleCircle(
                        loc.TypeCode,
                        loc.VolumePosition,
                        inset,
                        loc.Radius,
                        parent.Scene.ScreenPixelSizeInVolume,
                        parent.Scene.Viewport.Width,
                        parent.Scene.Viewport.Height,
                        Global.AnnotationSettings.AutoPolygonizeMinScreenAreaPercent))
                    continue;

                eligible.Add(loc);
            }

            return eligible;
        }

        /// <summary>Store objects for canvas views intersecting <paramref name="viewBounds"/>; used for candidates and background prompts.</summary>
        private IEnumerable<LocationObj> CollectVisibleLocationObjs(GridRectangle viewBounds)
        {
            SectionAnnotationsView sectionView = AnnotationOverlay.GetOrCreateAnnotationsForSection(parent.Section.Number);
            if (sectionView is null)
                return [];

            ICollection<LocationCanvasView> locations = sectionView.GetLocations(viewBounds);
            List<LocationObj> result = [];
            foreach (LocationCanvasView view in locations)
            {
                LocationObj loc = Store.Locations.GetObjectByID(view.ID, false);
                if (loc is not null)
                    result.Add(loc);
            }

            return result;
        }

        /// <summary>
        /// Cancels capture/encode/upload. Leaves already-uploaded SegmentImage work running.
        /// </summary>
        private void CancelUploadPhase()
        {
            SegmentationViewportSession? toCancel = null;
            lock (lifecycleLock)
            {
                try
                {
                    uploadCts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                uploadCts?.Dispose();
                uploadCts = null;
                toCancel = uploadSession;
                uploadSession = null;
            }

            if (toCancel is null)
                return;

            toCancel.CancelPendingWork();
            if (toCancel.CurrentImageId.HasValue)
                _ = toCancel.DeleteCurrentImageAsync();
        }

        /// <summary>
        /// Cancels in-flight SegmentImage and per-response CPU work (section change, dispose, Segment command).
        /// </summary>
        private void CancelProcessPhase()
        {
            List<ProcessBatch> batches;
            lock (lifecycleLock)
            {
                try
                {
                    processCts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                processCts?.Dispose();
                processCts = new CancellationTokenSource();
                batches = [.. processBatches];
                processBatches.Clear();
            }

            foreach (ProcessBatch batch in batches)
            {
                batch.Session.CancelPendingWork();
                _ = FinishProcessBatchAsync(batch);
            }
        }

        /// <summary>
        /// Finally of <see cref="RunBatchAsync"/>. If the session reached process
        /// phase, wait for response tasks then delete the server image; otherwise
        /// delete an unused upload.
        /// </summary>
        private async Task FinishUploadOrProcessBatchAsync(
            SegmentationViewportSession? localUploadSession,
            ProcessBatch? processBatch)
        {
            if (processBatch is not null)
            {
                await FinishProcessBatchAsync(processBatch).ConfigureAwait(false);
                return;
            }

            if (localUploadSession is null)
                return;

            lock (lifecycleLock)
            {
                if (ReferenceEquals(uploadSession, localUploadSession))
                    uploadSession = null;
            }

            localUploadSession.CancelPendingWork();
            if (localUploadSession.CurrentImageId.HasValue)
                await localUploadSession.DeleteCurrentImageAsync().ConfigureAwait(false);
        }

        /// <summary>Waits for remaining polygonize tasks, then DeleteImage so the SAM2 cache does not keep an orphan.</summary>
        private async Task FinishProcessBatchAsync(ProcessBatch batch)
        {
            try
            {
                if (batch.ResponseTasks.Count > 0)
                    await Task.WhenAll(batch.ResponseTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto polygonize response tasks failed: {ex.Message}");
            }
            finally
            {
                lock (lifecycleLock)
                {
                    processBatches.Remove(batch);
                }

                await batch.Session.DeleteCurrentImageAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Removes a proposal and disposes its mask texture if one exists.</summary>
        private void RemoveProposal(long locationId)
        {
            lock (proposalLock)
            {
                if (hoveredProposal?.LocationId == locationId)
                    hoveredProposal = null;

                if (proposals.TryGetValue(locationId, out AutoPolygonizeProposal existing))
                    existing.DisposeMaskOverlay();

                proposals.Remove(locationId);
            }
        }

        /// <summary>Drops GPU mask textures while keeping the outline proposals.</summary>
        private void DisposeAllMaskOverlays()
        {
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in proposals.Values)
                    proposal.DisposeMaskOverlay();
            }
        }

        /// <summary>Clears cache and overlay when a location is deleted or is no longer a circle.</summary>
        private void OnLocationsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
            {
                foreach (object item in e.OldItems)
                {
                    if (item is LocationObj loc)
                    {
                        cache.Remove(loc.ID);
                        RemoveProposal(loc.ID);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace && e.NewItems is not null)
            {
                foreach (object item in e.NewItems)
                {
                    if (item is LocationObj loc && loc.TypeCode != LocationType.CIRCLE)
                    {
                        cache.Remove(loc.ID);
                        RemoveProposal(loc.ID);
                    }
                }
            }
        }

        /// <summary>Live world bounds, or <see cref="lastViewBounds"/> if Scene is not ready (startup/teardown).</summary>
        private GridRectangle GetCurrentViewportBounds()
        {
            if (parent.Scene is not null)
                return parent.Scene.VisibleWorldBounds;

            return lastViewBounds;
        }
    }
}
