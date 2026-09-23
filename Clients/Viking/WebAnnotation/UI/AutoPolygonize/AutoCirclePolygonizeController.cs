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
    /// collected points still segment. Section changes cancel both phases. An
    /// interactive <see cref="SegmentationCommand"/> cancels proposal work but
    /// leaves an in-flight viewport upload so that command can adopt the same image.
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
        private readonly SharedViewportImageLease viewportImageLease;
        private readonly Dictionary<long, AutoPolygonizeProposal> proposals = [];
        private readonly object proposalLock = new();

        private bool enabled;
        private System.Timers.Timer? idleTimer;
        private System.Timers.Timer? confirmTimer;

        /// <summary>Viewport captured when the idle timer fired; compared again after <see cref="ConfirmDelayMs"/>.</summary>
        private Rectangle armedBounds;
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
        private Rectangle lastViewBounds;
        private AutoPolygonizeProposal? hoveredProposal;
        private readonly HashSet<string> overlapResubmitsInFlight = [];

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
            viewportImageLease = new SharedViewportImageLease(cache);
            cache.GeometryInvalidated += OnCacheGeometryInvalidated;
            cache.LocationForgotten += OnCacheLocationForgotten;
            cache.ImageLeaseReleased += OnImageLeaseReleased;
            cache.ImageLeaseReleased += viewportImageLease.Forget;
        }

        /// <summary>Cache that owns SAM2 image holds. Segmentation commands take and release holds here.</summary>
        internal AutoPolygonizeCache ImageCache => cache;

        /// <summary>Current-view upload shared with <see cref="SegmentationCommand"/>.</summary>
        internal SharedViewportImageLease ViewportImages => viewportImageLease;

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
        /// Drops overlays and cache entries for one Z when <see cref="SectionAnnotationsView"/> is evicted.
        /// Safe to call from the section-cache cleaner thread.
        /// </summary>
        public void ClearSection(int sectionNumber)
        {
            Action clear = () =>
            {
                DropProposalsForSection(sectionNumber);
                cache.ClearSection(sectionNumber);
                if (enabled)
                    parent.Invalidate();
            };

            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                clear();
            else
                dispatcher.BeginInvoke(clear);
        }

        /// <summary>
        /// Restarts settle when the view actually moved. Aborts capture/encode only;
        /// an already-uploaded batch keeps segmenting.
        /// </summary>
        public void OnCameraChanged()
        {
            if (!enabled)
                return;

            Rectangle current = GetCurrentViewportBounds();
            if (SegmentationViewportSession.AreViewportBoundsSimilar(lastViewBounds, current))
                return;

            lastViewBounds = current;
            lastCameraChangeUtc = DateTime.UtcNow;
            viewportImageLease.ForgetIfViewMoved(current, GetCurrentDownsample());
            CancelUploadPhase();
            CancelConfirmTimer();
            RestartIdleTimer();
        }

        /// <summary>
        /// Drops proposals and image leases from the previous segmentation server.
        /// When <paramref name="newEndpointIsUsable"/> and auto-polygonize is on, restarts
        /// the idle settle so the current view is uploaded to the new server.
        /// Call before the gRPC channel resets so DeleteImage still reaches the old server.
        /// </summary>
        public void OnSegmentationEndpointChanged(bool newEndpointIsUsable)
        {
            CancelUploadPhase();
            CancelProcessPhase();
            viewportImageLease.AbandonPublished();
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in proposals.Values)
                    proposal.DisposeMaskOverlay();

                proposals.Clear();
            }

            hoveredProposal = null;
            cache.Clear();
            if (enabled && newEndpointIsUsable)
                RestartIdleTimer();
            else
                StopSettleTimers();

            parent.Invalidate();
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
        /// Preference toggle for the debug SAM2 mask overlay.
        /// Turning it off disposes GPU textures. Turning it on attaches textures for
        /// proposals that already kept mask bytes, and asks the idle batch to segment
        /// again when those bytes were never stored. Prompt dots are a separate preference.
        /// </summary>
        public void OnOverlayMasksChanged(bool showMasks)
        {
            AutoPolygonizeProposal[] snapshot;
            lock (proposalLock)
                snapshot = [.. DistinctProposalsUnlocked()];

            if (!showMasks)
            {
                foreach (AutoPolygonizeProposal proposal in snapshot)
                    proposal.DisposeMaskOverlay();

                parent.Invalidate();
                return;
            }

            bool needsResegment = false;
            foreach (AutoPolygonizeProposal proposal in snapshot)
            {
                if (proposal.HasMaskSource)
                    proposal.EnsureMaskOverlay(parent.Device);
                else
                    needsResegment = true;
            }

            if (needsResegment)
                RequestMaskRefresh(snapshot);

            parent.Invalidate();
        }

        /// <summary>
        /// Drops the cache skip for on-screen proposals that have no mask bytes and restarts
        /// the idle settle so the next batch stores a mask. Outlines stay up until that batch publishes.
        /// </summary>
        private void RequestMaskRefresh(IReadOnlyList<AutoPolygonizeProposal> snapshot)
        {
            foreach (AutoPolygonizeProposal proposal in snapshot)
            {
                if (proposal.HasMaskSource)
                    continue;

                foreach (long locationId in proposal.LocationIds)
                    cache.InvalidateProposal(locationId);
            }

            if (enabled)
                RestartIdleTimer();
        }

        /// <summary>
        /// Preference toggle for green foreground and red background prompt dots.
        /// The points are already on each proposal, so this only redraws.
        /// </summary>
        public void OnOverlayPromptsChanged(bool showPrompts)
        {
            parent.Invalidate();
        }

        /// <summary>Asks the viewer to repaint proposals after a draw-only preference change.</summary>
        public void InvalidateOverlay()
        {
            parent.Invalidate();
        }

        /// <summary>Draws proposals for the current section when <see cref="ShouldShowProposals"/> is true.</summary>
        public void Draw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene)
        {
            if (!enabled || !ShouldShowProposals || parent.Section is null)
                return;

            RemoveProposalsThatAreNoLongerCircles();
            int sectionNumber = parent.Section.Number;
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in DistinctProposalsUnlocked())
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
        public bool TryHit(Vector2 worldPosition, out AutoPolygonizeProposal proposal, out double distance)
        {
            proposal = null;
            distance = double.MaxValue;
            if (!enabled || !ShouldShowProposals || parent.Section is null || parent.Camera is null)
                return false;

            RemoveProposalsThatAreNoLongerCircles();
            double threshold = parent.Camera.Downsample * AutoPolygonizeSelection.HitTestPixels;
            int sectionNumber = parent.Section.Number;

            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal candidate in DistinctProposalsUnlocked())
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
        public void UpdateHover(Vector2 worldPosition)
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

        /// <summary>
        /// Writes the proposal polygon onto the survivor location. For a same-cell group,
        /// unique Z-links move to the survivor and the other locations are deleted first.
        /// </summary>
        public void Accept(AutoPolygonizeProposal proposal)
        {
            if (proposal is null)
                return;

            List<LocationObj> members = [];
            foreach (long id in proposal.LocationIds)
            {
                LocationObj loc = Store.Locations.GetObjectByID(id, false);
                if (loc is not null)
                    members.Add(loc);
            }

            if (members.Count == 0)
            {
                RemoveProposal(proposal.LocationId);
                return;
            }

            long survivorId = LocationSiblingMerge.ChooseSurvivorId(members);
            LocationObj survivor = members.First(member => member.ID == survivorId);
            List<LocationObj> victims = [.. members.Where(member => member.ID != survivorId)];

            Polygon? toApply = CarveAgainstExistingPolygons(
                proposal.Polygon,
                proposal.LocationIds,
                survivor.VolumePosition,
                survivor.ParentID);
            if (toApply is null)
            {
                RemoveProposal(proposal.LocationId);
                parent.Invalidate();
                return;
            }

            toApply = AutoPolygonizeSelection.SimplifyProposal(
                toApply,
                AutoPolygonizeSelection.CreatedShapeSimplifyWorld(parent.Downsample));

            if (!LocationShapeUpdate.ApplyVolumePolygon(survivor, toApply, parent))
                return;

            IReadOnlyList<long> toLink = LocationSiblingMerge.UniqueNeighborIdsToTransfer(
                survivor.LinksCopy,
                proposal.LocationIds,
                victims.Select(victim => (IReadOnlyCollection<long>)victim.LinksCopy));
            foreach (long neighborId in toLink)
                Store.LocationLinks.CreateLink(survivorId, neighborId);

            foreach (LocationObj victim in victims)
                Store.Locations.Remove(victim);

            if (victims.Count > 0)
                AnnotationOverlay.SaveLocationsWithMessageBoxOnError();

            foreach (long id in proposal.LocationIds)
                cache.Remove(id);
            RemoveProposal(proposal.LocationId);
            CarveRemainingProposals(toApply, proposal.LocationIds);
            parent.Invalidate();
        }

        /// <summary>Hides the proposal until each involved location's LastModified changes.</summary>
        public void Dismiss(AutoPolygonizeProposal proposal)
        {
            if (proposal is null)
                return;

            foreach (long id in proposal.LocationIds)
            {
                LocationObj loc = Store.Locations.GetObjectByID(id, false);
                cache.Dismiss(
                    id,
                    proposal.SectionNumber,
                    loc?.LastModified ?? proposal.LastModified,
                    loc);
            }

            RemoveProposal(proposal.LocationId);
            parent.Invalidate();
        }

        /// <summary>
        /// Drops the overlay and cache entry. Used when TypeCode is no longer CIRCLE or
        /// when auto-segment becomes visible again after a convert command.
        /// </summary>
        private void ForgetLocation(long locationId)
        {
            cache.Remove(locationId);
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

            if (!IsWithinAutoSegmentDownsample())
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
        /// False when the camera is coarser than <see cref="Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>.
        /// Equality still runs, so downsample 8 is sent when the preference is 8.
        /// </summary>
        private bool IsWithinAutoSegmentDownsample()
        {
            return GetCurrentDownsample() <= Global.AnnotationSettings.AutoPolygonizeMaxDownsample;
        }

        /// <summary>
        /// Restarts the idle wait when auto-segment is on. Used after the max-downsample preference changes
        /// so a view that is now allowed can run without a camera nudge.
        /// </summary>
        public void RequestIdlePass()
        {
            if (!enabled)
                return;

            RestartIdleTimer();
        }

        /// <summary>
        /// One capture/upload plus sequential SegmentImage calls. GPU capture stays
        /// on the session UI path; polygonize runs on a task per response. A camera
        /// move after upload does not cancel the process token.
        /// </summary>
        private async Task RunBatchAsync()
        {
            if (!enabled || !Global.IsSegmentationServiceAvailable || parent.Scene is null || parent.Section is null)
                return;

            if (!IsWithinAutoSegmentDownsample())
                return;

            if (parent.CurrentCommand is SegmentationCommand)
            {
                CancelProcessPhase();
                if (!viewportImageLease.HasInFlight)
                    CancelUploadPhase();
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
                Rectangle viewBounds = localUploadSession.ViewportBounds;
                Rectangle inset = AutoPolygonizeSelection.InsetBounds(viewBounds);
                List<LocationObj> candidates = CollectEligibleCircles(viewBounds, inset);
                if (candidates.Count == 0)
                {
                    ScheduleSavedSiblingOverlapScan();
                    return;
                }

                long candidateCollectionMs = stepTimer.ElapsedMilliseconds;
                IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
                int sectionNumber = parent.Section.Number;
                double downsample = parent.Camera.Downsample;
                double simplifyTolerance = AutoPolygonizeSelection.MaskContourToleranceWorld(parent.Downsample);

                stepTimer.Restart();
                AutoPolygonizeUploadContext? uploadContext = await CaptureSharedViewportAsync(
                    localUploadSession,
                    viewBounds,
                    downsample,
                    uploadToken).ConfigureAwait(false);
                if (uploadContext is null)
                    return;

                if (uploadToken.IsCancellationRequested || !enabled || parent.CurrentCommand is SegmentationCommand)
                    return;

                cache.AcquireBatchHold(uploadContext.Value.ImageId);

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

                    if (!cache.ShouldProcess(circle.ID, sectionNumber, circle.LastModified, circle.TypeCode, downsample))
                        continue;

                    int generation = cache.MarkPending(circle.ID, sectionNumber, circle, uploadContext);

                    Stopwatch proposalTimer = Stopwatch.StartNew();
                    IReadOnlyList<Vector2> foreground = CircleSegmentationPrompts.ToVolumePoints(
                        CircleSegmentationPrompts.CreateMosaicForegroundPoints(new Circle(circle.Position, circle.Radius)),
                        transform);

                    if (foreground.Count == 0)
                        continue;

                    IReadOnlyList<Vector2> background = CircleSegmentationPrompts.CreateOtherStructureBackgroundVolumePoints(
                        CollectVisibleLocationObjs(viewBounds),
                        transform,
                        foreground,
                        Global.AnnotationSettings.SegmentationPointRadius * downsample,
                        [circle.ID],
                        circle.ParentID);
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
                            foreground,
                            background,
                            response,
                            promptMs,
                            segmentMs,
                            proposalTimer,
                            batchTimer,
                            batchId,
                            generation,
                            requireMatchingLiveView: true,
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

                ScheduleSavedSiblingOverlapScan();
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
        /// An empty union is a LastModified skip (no overlay) so the next idle batch
        /// does not SegmentImage the same circle again. GPU mask textures are created
        /// later on the UI thread in <see cref="PublishProposal"/>.
        /// </summary>
        private void ProcessResponse(
            SegmentationViewportSession session,
            LocationObj circle,
            int sectionNumber,
            double downsample,
            double simplifyTolerance,
            IReadOnlyList<Vector2> foreground,
            IReadOnlyList<Vector2> background,
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentationResponse response,
            long promptMs,
            long segmentMs,
            Stopwatch proposalTimer,
            Stopwatch batchTimer,
            long batchId,
            int generation,
            bool requireMatchingLiveView,
            CancellationToken processToken)
        {
            if (processToken.IsCancellationRequested || !enabled)
                return;

            if (!IsStillCircle(circle.ID) || !cache.IsGenerationCurrent(circle.ID, generation))
                return;

            if (requireMatchingLiveView)
            {
                Rectangle liveBounds = session.GetLiveViewportBoundsAsync().GetAwaiter().GetResult();
                if (parent.Section is null ||
                    parent.Section.Number != sectionNumber ||
                    !SegmentationViewportSession.ShouldUploadEncodedCapture(session.ViewportBounds, liveBounds))
                {
                    Debug.WriteLine(
                        $"[SegmentationProfile] Auto batch={batchId} location={circle.ID} dropped: view moved before publish");
                    return;
                }
            }
            else if (parent.Section is null || parent.Section.Number != sectionNumber)
            {
                return;
            }

            Stopwatch polygonTimer = Stopwatch.StartNew();
            IReadOnlyList<Polygon> polygons = session.CreatePolygonsFromResponse(
                response,
                preserveHolesContainingWorldPoints: background);
            Polygon polygon = polygons.FirstOrDefault();
            long polygonMs = polygonTimer.ElapsedMilliseconds;
            if (polygon is null)
            {
                if (IsStillCircle(circle.ID) && cache.IsGenerationCurrent(circle.ID, generation))
                    RememberEmptyMask(circle, sectionNumber, session, downsample);
                return;
            }

            AutoPolygonizeMaskOverlay? maskOverlay = AutoPolygonizeMaskOverlay.TryCreate(session, response);

            Stopwatch renderPreparationTimer = Stopwatch.StartNew();
            int verticesBeforeSimplify = polygon.TotalUniqueVertices;
            polygon = AutoPolygonizeSelection.SimplifyProposal(polygon, simplifyTolerance);
            polygon = CarveAgainstExistingPolygons(polygon, [circle.ID], circle.VolumePosition, circle.ParentID);
            if (polygon is null)
            {
                if (IsStillCircle(circle.ID) && cache.IsGenerationCurrent(circle.ID, generation))
                    RememberEmptyMask(circle, sectionNumber, session, downsample);
                return;
            }

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
                maskOverlay,
                locationIds: [circle.ID],
                parentId: circle.ParentID,
                foregroundPrompts: foreground,
                backgroundPrompts: background);
            long renderPreparationMs = renderPreparationTimer.ElapsedMilliseconds;

            if (!IsStillCircle(circle.ID) || !cache.IsGenerationCurrent(circle.ID, generation))
            {
                proposal.DisposeMaskOverlay();
                return;
            }

            cache.RememberProposal(
                circle.ID,
                sectionNumber,
                circle.LastModified,
                circle.TypeCode,
                Store.Locations.GetObjectByID(circle.ID, false),
                TryCreateUploadContext(session, downsample),
                downsample);
            PublishProposal(proposal);
            Debug.WriteLine(
                $"[SegmentationProfile] Auto batch={batchId} location={circle.ID} ready-to-draw " +
                $"prompts={promptMs}ms segmentRpc={segmentMs}ms polygonize={polygonMs}ms " +
                $"simplifyAndViews={renderPreparationMs}ms vertices={verticesBeforeSimplify}->{polygon.TotalUniqueVertices} " +
                $"proposal={proposalTimer.ElapsedMilliseconds}ms " +
                $"batchElapsed={batchTimer.ElapsedMilliseconds}ms foreground={foreground.Count} background={background.Count}");
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
                if (!enabled || !AreProposalMembersValid(proposal))
                {
                    proposal.DisposeMaskOverlay();
                    return;
                }

                lock (proposalLock)
                {
                    HashSet<AutoPolygonizeProposal> replaced = [];
                    foreach (long id in proposal.LocationIds)
                    {
                        if (proposals.TryGetValue(id, out AutoPolygonizeProposal existing) &&
                            !ReferenceEquals(existing, proposal))
                        {
                            replaced.Add(existing);
                        }
                    }

                    foreach (AutoPolygonizeProposal old in replaced)
                    {
                        old.DisposeMaskOverlay();
                        if (ReferenceEquals(hoveredProposal, old))
                            hoveredProposal = null;
                        foreach (long id in old.LocationIds)
                            proposals.Remove(id);
                    }

                    if (Global.AnnotationSettings.AutoPolygonizeOverlayMasks)
                        proposal.AttachMaskOverlay(parent.Device);

                    foreach (long id in proposal.LocationIds)
                        proposals[id] = proposal;
                }

                parent.Invalidate();
                TryBeginOverlapResubmit(proposal);
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
            Rectangle? mosaicBounds = parent.Scene.VisibleWorldBounds.ApproximateVisibleMosaicBounds(sectionView.mapper);
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
        /// Circles whose center is at least 5% from each edge, whose disk is fully
        /// on screen, large enough, and not already proposed or dismissed for this LastModified.
        /// A proposal completed at a coarser view is eligible again when the camera is at least
        /// twice as fine. Ordered nearest-to-farthest from <paramref name="viewBounds"/> center.
        /// </summary>
        private List<LocationObj> CollectEligibleCircles(Rectangle viewBounds, Rectangle inset)
        {
            List<LocationObj> eligible = [];
            double nmPerWorld = Global.Scale.X;
            double minRadiusNm = Global.AnnotationSettings.AutoPolygonizeMinRadiusNanometers;
            foreach (LocationObj loc in CollectVisibleLocationObjs(viewBounds))
            {
                if (!cache.ShouldProcess(loc.ID, loc.Section, loc.LastModified, loc.TypeCode, GetCurrentDownsample()))
                    continue;

                if (!AutoPolygonizeSelection.IsEligibleCircle(
                        loc.TypeCode,
                        loc.VolumePosition,
                        inset,
                        viewBounds,
                        loc.Radius,
                        nmPerWorld,
                        minRadiusNm))
                    continue;

                eligible.Add(loc);
            }

            return AutoPolygonizeSelection.OrderByDistanceFromCenter(
                eligible,
                loc => loc.VolumePosition,
                viewBounds.Center);
        }

        /// <summary>Store objects for canvas views intersecting <paramref name="viewBounds"/>; used for candidates and background prompts.</summary>
        private IEnumerable<LocationObj> CollectVisibleLocationObjs(Rectangle viewBounds)
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
            viewportImageLease.AbandonInFlight();
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
            if (localUploadSession.CurrentImageId is ulong uploadedId && !cache.IsImageHeld(uploadedId))
                await localUploadSession.DeleteCurrentImageAsync().ConfigureAwait(false);
            else
                localUploadSession.ClearImageId();
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

                if (batch.Session.CurrentImageId is ulong imageId)
                {
                    batch.Session.ClearImageId();
                    cache.ReleaseBatchHold(imageId);
                }
            }
        }

        /// <summary>
        /// True when the store still has this ID as a circle, or (for a grouped overlay)
        /// as a closed 2D polygon of the same cell. Single-id circle proposals still drop
        /// once Convert to Polygon commits.
        /// </summary>
        private static bool IsGroupMemberValid(long locationId)
        {
            LocationObj live = Store.Locations.GetObjectByID(locationId, false);
            if (live is null)
                return false;

            return live.TypeCode == LocationType.CIRCLE || live.TypeCode.AllowsInteriorHoles();
        }

        /// <summary>
        /// Single-id overlays stay circle-only. Group overlays may mix circles being
        /// converted with already-saved POLYGON/CURVEPOLYGON siblings.
        /// </summary>
        private static bool AreProposalMembersValid(AutoPolygonizeProposal proposal)
        {
            if (proposal.LocationIds.Count == 1)
                return IsStillCircle(proposal.LocationIds[0]);

            return proposal.LocationIds.All(IsGroupMemberValid);
        }

        /// <summary>True only when the store still has this ID as a circle. In-flight publishes must not outlive Convert to Polygon.</summary>
        private static bool IsStillCircle(long locationId)
        {
            LocationObj live = Store.Locations.GetObjectByID(locationId, false);
            return live is not null && live.TypeCode == LocationType.CIRCLE;
        }

        /// <summary>
        /// Drops overlays whose location was converted to a polygon (or deleted) while
        /// auto-segment was hidden. Group overlays that include saved siblings stay.
        /// </summary>
        private void RemoveProposalsThatAreNoLongerCircles()
        {
            AutoPolygonizeProposal[] stale;
            lock (proposalLock)
            {
                stale = [.. DistinctProposalsUnlocked().Where(item => !AreProposalMembersValid(item))];
            }

            foreach (AutoPolygonizeProposal proposal in stale)
                RemoveProposal(proposal.LocationId);
        }

        /// <summary>Cache dropped LastModified skip after a geometry commit. Overlay is stale; force one SegmentImage if still in view.</summary>
        private void OnCacheGeometryInvalidated(long locationId, int sectionNumber, int generation)
        {
            RemoveProposal(locationId);
            if (!enabled)
                return;

            parent.Invalidate();
            _ = RunForLocationAsync(locationId, sectionNumber, generation);
        }

        private void OnCacheLocationForgotten(long locationId)
        {
            RemoveProposal(locationId);
            if (enabled)
                parent.Invalidate();
        }

        private void OnImageLeaseReleased(ulong imageId)
        {
            _ = DeleteLeasedImageAsync(imageId);
        }

        /// <summary>Creates a short-lived session only to DeleteImage a lease the cache no longer holds.</summary>
        private async Task DeleteLeasedImageAsync(ulong imageId)
        {
            var session = new SegmentationViewportSession(parent);
            if (!session.TryInitializeClient())
                return;

            await session.DeleteImageByIdAsync(imageId).ConfigureAwait(false);
        }

        /// <summary>
        /// Single-ID refresh after a translate/scale/resize. Reuses the leased SAM2 image when zoom
        /// and uploaded bounds still apply; otherwise uploads the current view. NotFound re-upload
        /// is owned by <see cref="SegmentationViewportSession.SegmentAsync"/>.
        /// </summary>
        private async Task RunForLocationAsync(long locationId, int sectionNumber, int generation)
        {
            if (!enabled || !Global.IsSegmentationServiceAvailable || parent.Scene is null || parent.Section is null)
                return;

            if (!IsWithinAutoSegmentDownsample())
                return;

            if (parent.Section.Number != sectionNumber || parent.CurrentCommand is SegmentationCommand)
                return;

            LocationObj circle = Store.Locations.GetObjectByID(locationId, false);
            if (circle is null || circle.TypeCode != LocationType.CIRCLE)
            {
                cache.Remove(locationId);
                return;
            }

            Rectangle viewBounds = GetCurrentViewportBounds();
            Rectangle inset = AutoPolygonizeSelection.InsetBounds(viewBounds);
            if (!AutoPolygonizeSelection.IsEligibleCircle(
                    circle.TypeCode,
                    circle.VolumePosition,
                    inset,
                    viewBounds,
                    circle.Radius,
                    Global.Scale.X,
                    Global.AnnotationSettings.AutoPolygonizeMinRadiusNanometers))
            {
                cache.Remove(locationId);
                return;
            }

            if (!cache.IsGenerationCurrent(locationId, generation))
                return;

            CancellationToken processToken;
            lock (lifecycleLock)
            {
                processToken = processCts.Token;
            }

            SegmentationViewportSession session = new(parent);
            if (!session.TryInitializeClient())
                return;

            double downsample = GetCurrentDownsample();
            bool acquiredHold = false;
            ulong? holdImageId = null;
            try
            {
                bool reuse = cache.TryGetUploadContext(locationId, out AutoPolygonizeUploadContext uploadContext) &&
                             AutoPolygonizeSelection.CanReuseUploadedImage(uploadContext, downsample, circle.VolumePosition);
                if (reuse)
                {
                    session.AdoptUploadedImage(
                        uploadContext.ImageId,
                        uploadContext.WorldBounds,
                        uploadContext.Width,
                        uploadContext.Height);
                    if (SharedViewportImageLease.CanReuse(uploadContext, viewBounds, downsample))
                        viewportImageLease.Publish(uploadContext);
                }
                else
                {
                    AutoPolygonizeUploadContext? uploaded = await CaptureSharedViewportAsync(
                        session,
                        viewBounds,
                        downsample,
                        processToken).ConfigureAwait(false);
                    if (uploaded is null)
                        return;

                    cache.AcquireBatchHold(uploaded.Value.ImageId);
                    acquiredHold = true;
                    holdImageId = uploaded.Value.ImageId;
                }

                IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
                IReadOnlyList<Vector2> foreground = CircleSegmentationPrompts.ToVolumePoints(
                    CircleSegmentationPrompts.CreateMosaicForegroundPoints(new Circle(circle.Position, circle.Radius)),
                    transform);
                if (foreground.Count == 0)
                    return;

                IReadOnlyList<Vector2> background = CircleSegmentationPrompts.CreateOtherStructureBackgroundVolumePoints(
                    CollectVisibleLocationObjs(viewBounds),
                    transform,
                    foreground,
                    Global.AnnotationSettings.SegmentationPointRadius * downsample,
                    [circle.ID],
                    circle.ParentID);

                Stopwatch proposalTimer = Stopwatch.StartNew();
                var response = await session.SegmentAsync(foreground, background, processToken).ConfigureAwait(false);
                if (response is null)
                    return;

                downsample = GetCurrentDownsample();
                AutoPolygonizeUploadContext? afterSegment = TryCreateUploadContext(session, downsample);
                if (afterSegment.HasValue && holdImageId is ulong held && held != afterSegment.Value.ImageId)
                {
                    cache.AcquireBatchHold(afterSegment.Value.ImageId);
                    cache.ReleaseBatchHold(held);
                    holdImageId = afterSegment.Value.ImageId;
                }

                int liveGeneration = cache.MarkPending(circle.ID, sectionNumber, circle, afterSegment);
                ProcessResponse(
                    session,
                    circle,
                    sectionNumber,
                    downsample,
                    AutoPolygonizeSelection.MaskContourToleranceWorld(parent.Downsample),
                    foreground,
                    background,
                    response,
                    0,
                    0,
                    proposalTimer,
                    proposalTimer,
                    0,
                    liveGeneration,
                    requireMatchingLiveView: false,
                    processToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto polygonize single-ID refresh failed: {ex.Message}");
            }
            finally
            {
                if (acquiredHold && holdImageId is ulong id)
                    cache.ReleaseBatchHold(id);
                else if (session.CurrentImageId is ulong leftover && !cache.IsImageHeld(leftover))
                    await session.DeleteCurrentImageAsync().ConfigureAwait(false);
                else
                    session.ClearImageId();
            }
        }

        /// <summary>
        /// LastModified skip without publishing an overlay. Empty SAM2 unions must not
        /// stay pending or the next idle/confirm batch will SegmentImage the same circle.
        /// </summary>
        private void RememberEmptyMask(
            LocationObj circle,
            int sectionNumber,
            SegmentationViewportSession session,
            double downsample)
        {
            cache.RememberProposal(
                circle.ID,
                sectionNumber,
                circle.LastModified,
                circle.TypeCode,
                Store.Locations.GetObjectByID(circle.ID, false),
                TryCreateUploadContext(session, downsample),
                downsample);
        }

        /// <summary>
        /// Overlap-resubmit empty union: skip every involved ID so the group is not
        /// SegmentImage'd again until a member's LastModified changes.
        /// </summary>
        private void RememberEmptyMaskForIds(
            IReadOnlyList<long> locationIds,
            int sectionNumber,
            DateTime lastModifiedFallback,
            SegmentationViewportSession session,
            double downsample)
        {
            AutoPolygonizeUploadContext? upload = TryCreateUploadContext(session, downsample);
            foreach (long id in locationIds)
            {
                LocationObj loc = Store.Locations.GetObjectByID(id, false);
                cache.RememberProposal(
                    id,
                    sectionNumber,
                    loc?.LastModified ?? lastModifiedFallback,
                    loc?.TypeCode ?? LocationType.CIRCLE,
                    loc,
                    upload,
                    downsample);
            }
        }

        private static AutoPolygonizeUploadContext? TryCreateUploadContext(SegmentationViewportSession session, double downsample)
            => SharedViewportImageLease.TryCreateContext(session, downsample);

        /// <summary>
        /// Reuses a viewport-similar upload or runs one capture shared with <see cref="SegmentationCommand"/>.
        /// Installs the image on <paramref name="session"/>. The lease holds the id; callers add their own hold while segmenting.
        /// </summary>
        private async Task<AutoPolygonizeUploadContext?> CaptureSharedViewportAsync(
            SegmentationViewportSession session,
            Rectangle viewBounds,
            double downsample,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return null;

            AutoPolygonizeUploadContext? context = await viewportImageLease.GetOrUploadAsync(
                viewBounds,
                downsample,
                () => UploadSessionForLeaseAsync(session, viewBounds, downsample, cancellationToken)).ConfigureAwait(false);

            if (context is null && !cancellationToken.IsCancellationRequested)
            {
                context = await viewportImageLease.GetOrUploadAsync(
                    viewBounds,
                    downsample,
                    () => UploadSessionForLeaseAsync(session, viewBounds, downsample, cancellationToken)).ConfigureAwait(false);
            }

            if (context is not { IsUsable: true } ready)
                return null;

            if (!SharedViewportImageLease.CanReuse(ready, GetCurrentViewportBounds(), GetCurrentDownsample()))
            {
                viewportImageLease.ForgetIfViewMoved(GetCurrentViewportBounds(), GetCurrentDownsample());
                return null;
            }

            if (session.CurrentImageId != ready.ImageId)
            {
                session.AdoptUploadedImage(
                    ready.ImageId,
                    ready.WorldBounds,
                    ready.Width,
                    ready.Height);
            }

            return ready;
        }

        /// <summary>
        /// Runs only for the lease starter. Joined callers wait on the same task and then adopt.
        /// </summary>
        private static async Task<AutoPolygonizeUploadContext?> UploadSessionForLeaseAsync(
            SegmentationViewportSession session,
            Rectangle viewBounds,
            double downsample,
            CancellationToken cancellationToken)
        {
            session.ViewportBounds = viewBounds;
            if (!await session.UploadCurrentImageAsync(cancellationToken).ConfigureAwait(false))
                return null;

            return SharedViewportImageLease.TryCreateContext(session, downsample);
        }

        /// <summary>Removes a proposal overlay. Cache membership and subscriptions stay with <see cref="AutoPolygonizeCache"/>.</summary>
        private void RemoveProposal(long locationId)
        {
            lock (proposalLock)
            {
                if (!proposals.TryGetValue(locationId, out AutoPolygonizeProposal existing))
                    return;

                if (ReferenceEquals(hoveredProposal, existing))
                    hoveredProposal = null;

                existing.DisposeMaskOverlay();
                foreach (long id in existing.LocationIds)
                    proposals.Remove(id);
                proposals.Remove(locationId);
            }
        }

        private IEnumerable<AutoPolygonizeProposal> DistinctProposalsUnlocked() =>
            proposals.Values.Distinct();

        /// <summary>
        /// After a publish, if other same-cell overlays or already-saved sibling polygons
        /// nested-contain or cross this one, start one combined SegmentImage.
        /// At most <see cref="AutoPolygonizeSelection.MaxOverlapResubmitRound"/> rounds.
        /// </summary>
        private void TryBeginOverlapResubmit(AutoPolygonizeProposal seed)
        {
            if (seed.ParentID is null || seed.OverlapResubmitRound >= AutoPolygonizeSelection.MaxOverlapResubmitRound)
                return;

            List<AutoPolygonizeProposal> distinct;
            lock (proposalLock)
            {
                distinct = [.. DistinctProposalsUnlocked().Where(item => item.SectionNumber == seed.SectionNumber)];
            }

            HashSet<long> proposalIds = [.. distinct.SelectMany(item => item.LocationIds)];
            List<OverlapCandidate> candidates = [.. distinct.Select(ToOverlapCandidate)];
            candidates.AddRange(AutoPolygonizeSelection.CollectSavedSameCellPolygonCandidates(
                CollectVisibleLocationObjs(GetCurrentViewportBounds()),
                seed.ParentID.Value,
                seed.SectionNumber,
                proposalIds,
                parent.Section?.ActiveSectionToVolumeTransform));

            OverlapCandidate seedCandidate = ToOverlapCandidate(seed);
            List<OverlapCandidate> component = AutoPolygonizeSelection.CollectOverlappingSameCellComponent(
                seedCandidate,
                candidates);
            BeginOverlapResubmit(component, distinct);
        }

        /// <summary>
        /// Starts one combined SegmentImage for a same-cell component of 2+ locations.
        /// Skips when this exact ID set is already a grouped overlay.
        /// </summary>
        private void BeginOverlapResubmit(
            List<OverlapCandidate> component,
            List<AutoPolygonizeProposal> distinct)
        {
            long[] locationIds = [.. component.SelectMany(item => item.LocationIds).Distinct().OrderBy(id => id)];
            if (locationIds.Length < 2)
                return;

            List<AutoPolygonizeProposal> members = [.. distinct.Where(item =>
                item.LocationIds.Any(id => locationIds.Contains(id)))];
            int currentRound = members.Count == 0 ? 0 : members.Max(item => item.OverlapResubmitRound);
            if (currentRound >= AutoPolygonizeSelection.MaxOverlapResubmitRound)
                return;

            bool alreadyGrouped = members.Count == 1
                && members[0].LocationIds.Count == locationIds.Length
                && members[0].LocationIds.All(locationIds.Contains)
                && members[0].OverlapResubmitRound >= 1;
            if (alreadyGrouped)
                return;

            string key = string.Join(",", locationIds);
            lock (overlapResubmitsInFlight)
            {
                if (!overlapResubmitsInFlight.Add(key))
                    return;
            }

            _ = ResubmitOverlapGroupAsync(members, locationIds, currentRound + 1, key);
        }

        /// <summary>
        /// Visible saved POLYGON/CURVEPOLYGON fills of the same cell that overlap.
        /// Used when every sibling is already converted so no circle proposal exists.
        /// </summary>
        private void ScheduleSavedSiblingOverlapScan()
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            dispatcher?.BeginInvoke(new Action(TryBeginSavedSiblingOverlapResubmits));
        }

        /// <summary>
        /// One grouped SAM2 resubmit per overlapping saved-sibling component in the live view.
        /// Called from the UI thread after an idle batch (including a batch with no circles).
        /// </summary>
        private void TryBeginSavedSiblingOverlapResubmits()
        {
            if (!enabled || parent.Section is null)
                return;

            int sectionNumber = parent.Section.Number;
            IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
            IReadOnlyList<LocationObj> visible = [.. CollectVisibleLocationObjs(GetCurrentViewportBounds())];

            List<AutoPolygonizeProposal> distinct;
            lock (proposalLock)
            {
                distinct = [.. DistinctProposalsUnlocked().Where(item => item.SectionNumber == sectionNumber)];
            }

            HashSet<long> scannedParents = [];
            foreach (LocationObj location in visible)
            {
                if (location?.ParentID is not long parentId || !scannedParents.Add(parentId))
                    continue;

                List<OverlapCandidate> saved = AutoPolygonizeSelection.CollectSavedSameCellPolygonCandidates(
                    visible,
                    parentId,
                    sectionNumber,
                    excludeLocationIds: null,
                    transform);
                foreach (List<OverlapCandidate> component in AutoPolygonizeSelection.CollectOverlappingSameCellComponents(saved))
                    BeginOverlapResubmit(component, distinct);
            }
        }

        private static OverlapCandidate ToOverlapCandidate(AutoPolygonizeProposal proposal) =>
            new(proposal.LocationIds, proposal.ParentID, proposal.SectionNumber, proposal.Polygon);

        /// <summary>
        /// Proposal rings plus saved sibling polygons that are in the group but have no overlay yet.
        /// </summary>
        private List<Polygon> CollectOverlapPromptPolygons(
            List<AutoPolygonizeProposal> members,
            long[] locationIds)
        {
            List<Polygon> polygons = [];
            HashSet<long> fromProposals = [];
            foreach (AutoPolygonizeProposal member in members)
            {
                polygons.Add(member.Polygon);
                foreach (long id in member.LocationIds)
                    fromProposals.Add(id);
            }

            IVolumeToSectionTransform? transform = parent.Section?.ActiveSectionToVolumeTransform;
            foreach (long id in locationIds)
            {
                if (fromProposals.Contains(id))
                    continue;

                LocationObj loc = Store.Locations.GetObjectByID(id, false);
                if (AutoPolygonizeSelection.TryGetVolumePolygon(loc, transform, out Polygon? saved) && saved is not null)
                    polygons.Add(saved);
            }

            return polygons;
        }

        /// <summary>
        /// Removes other-structure POLYGON/CURVEPOLYGON area from <paramref name="proposed"/>.
        /// Same-cell siblings are excluded so they can be grouped instead of split.
        /// </summary>
        private Polygon? CarveAgainstExistingPolygons(
            Polygon proposed,
            IReadOnlyCollection<long> excludeLocationIds,
            Vector2 keepPoint,
            long? excludeParentId = null)
        {
            if (proposed is null || parent.Section is null)
                return proposed;

            List<Polygon> existing = AutoPolygonizeSelection.CollectOverlappingExistingPolygons(
                CollectVisibleLocationObjs(proposed.BoundingBox),
                proposed,
                excludeLocationIds,
                parent.Section.ActiveSectionToVolumeTransform,
                parent.Section.Number,
                excludeParentId);
            return AutoPolygonizeSelection.SubtractOverlappingPolygons(proposed, existing, keepPoint);
        }

        /// <summary>
        /// After one proposal is accepted, recarve every remaining overlay against the
        /// current store so a later accept does not write a stale overlapping ring.
        /// </summary>
        private void CarveRemainingProposals(Polygon accepted, IReadOnlyCollection<long> acceptedLocationIds)
        {
            List<AutoPolygonizeProposal> others;
            lock (proposalLock)
            {
                others =
                [
                    .. DistinctProposalsUnlocked()
                        .Where(item => !item.LocationIds.Any(acceptedLocationIds.Contains))
                ];
            }

            if (others.Count == 0)
                return;

            double downsample = GetCurrentDownsample();
            foreach (AutoPolygonizeProposal other in others)
            {
                if (!other.Polygon.Intersects(accepted))
                    continue;

                LocationObj seed = Store.Locations.GetObjectByID(other.LocationId, false);
                Vector2 keep = seed is not null ? seed.VolumePosition : other.Polygon.Centroid;
                Polygon? carved = CarveAgainstExistingPolygons(other.Polygon, other.LocationIds, keep, other.ParentID);
                if (carved is null)
                {
                    RemoveProposal(other.LocationId);
                    continue;
                }

                if (!ReferenceEquals(carved, other.Polygon))
                    other.ReplacePolygon(carved, downsample);
            }
        }

        /// <summary>
        /// One SegmentImage using points from the overlapping polygons. Replaces the member overlays
        /// with a single group proposal stored under every involved location ID.
        /// </summary>
        private async Task ResubmitOverlapGroupAsync(
            List<AutoPolygonizeProposal> members,
            long[] locationIds,
            int overlapRound,
            string inFlightKey)
        {
            try
            {
                if (!enabled || parent.Section is null || parent.Scene is null)
                    return;

                if (!IsWithinAutoSegmentDownsample())
                    return;

                CancellationToken processToken;
                lock (lifecycleLock)
                {
                    processToken = processCts.Token;
                }

                if (processToken.IsCancellationRequested)
                    return;

                SegmentationViewportSession session = new(parent);
                if (!session.TryInitializeClient())
                    return;

                double downsample = GetCurrentDownsample();
                bool acquiredHold = false;
                ulong? holdImageId = null;
                try
                {
                    bool reused = false;
                    foreach (long id in locationIds)
                    {
                        if (cache.TryGetUploadContext(id, out AutoPolygonizeUploadContext upload) &&
                            upload.IsUsable)
                        {
                            session.AdoptUploadedImage(
                                upload.ImageId,
                                upload.WorldBounds,
                                upload.Width,
                                upload.Height);
                            if (SharedViewportImageLease.CanReuse(upload, GetCurrentViewportBounds(), downsample))
                                viewportImageLease.Publish(upload);
                            reused = true;
                            break;
                        }
                    }

                    if (!reused)
                    {
                        AutoPolygonizeUploadContext? uploaded = await CaptureSharedViewportAsync(
                            session,
                            GetCurrentViewportBounds(),
                            downsample,
                            processToken).ConfigureAwait(false);
                        if (uploaded is null)
                            return;

                        cache.AcquireBatchHold(uploaded.Value.ImageId);
                        acquiredHold = true;
                        holdImageId = uploaded.Value.ImageId;
                    }

                    IReadOnlyList<Polygon> promptPolygons = CollectOverlapPromptPolygons(members, locationIds);
                    IReadOnlyList<Vector2> foreground = CircleSegmentationPrompts.CreateForegroundPointsFromPolygons(
                        promptPolygons);
                    if (foreground.Count == 0)
                        return;

                    LocationObj? firstLoc = Store.Locations.GetObjectByID(locationIds[0], false);
                    int sectionNumber = members.Count > 0 ? members[0].SectionNumber : firstLoc?.Section ?? parent.Section.Number;
                    long? parentId = members.Count > 0 ? members[0].ParentID : firstLoc?.ParentID;
                    if (parentId is null)
                        return;

                    DateTime lastModified = members.Count > 0
                        ? members.Max(member => member.LastModified)
                        : locationIds.Select(id => Store.Locations.GetObjectByID(id, false)?.LastModified ?? DateTime.MinValue).Max();
                    double circleRadius = members.Count > 0
                        ? members.Max(member => member.CircleRadius)
                        : locationIds.Select(id => Store.Locations.GetObjectByID(id, false)?.Radius ?? 0).DefaultIfEmpty(0).Max();

                    Rectangle viewBounds = GetCurrentViewportBounds();
                    HashSet<long> involved = [.. locationIds];
                    IReadOnlyList<Vector2> background = CircleSegmentationPrompts.CreateOtherStructureBackgroundVolumePoints(
                            CollectVisibleLocationObjs(viewBounds),
                            parent.Section.ActiveSectionToVolumeTransform,
                            foreground,
                            Global.AnnotationSettings.SegmentationPointRadius * downsample,
                            involved,
                            parentId);

                    var response = await session.SegmentAsync(foreground, background, processToken).ConfigureAwait(false);
                    if (response is null)
                        return;

                    if (!locationIds.All(IsGroupMemberValid))
                        return;

                    IReadOnlyList<Polygon> polygons = session.CreatePolygonsFromResponse(
                        response,
                        preserveHolesContainingWorldPoints: background);
                    Polygon polygon = polygons.FirstOrDefault();
                    if (polygon is null)
                    {
                        RememberEmptyMaskForIds(
                            locationIds,
                            sectionNumber,
                            lastModified,
                            session,
                            downsample);
                        return;
                    }

                    double simplifyTolerance = AutoPolygonizeSelection.MaskContourToleranceWorld(parent.Downsample);
                    polygon = AutoPolygonizeSelection.SimplifyProposal(polygon, simplifyTolerance);
                    LocationObj? keepLocation = Store.Locations.GetObjectByID(locationIds[0], false);
                    polygon = CarveAgainstExistingPolygons(
                        polygon,
                        locationIds,
                        keepLocation?.VolumePosition ?? polygon.Centroid,
                        parentId);
                    if (polygon is null)
                    {
                        RememberEmptyMaskForIds(
                            locationIds,
                            sectionNumber,
                            lastModified,
                            session,
                            downsample);
                        return;
                    }

                    AutoPolygonizeMaskOverlay? maskOverlay = AutoPolygonizeMaskOverlay.TryCreate(session, response);

                    AutoPolygonizeUploadContext? uploadContext = TryCreateUploadContext(session, downsample);
                    foreach (long id in locationIds)
                    {
                        LocationObj loc = Store.Locations.GetObjectByID(id, false);
                        cache.RememberProposal(
                            id,
                            sectionNumber,
                            loc?.LastModified ?? lastModified,
                            loc?.TypeCode ?? LocationType.CIRCLE,
                            loc,
                            uploadContext,
                            downsample);
                    }

                    AutoPolygonizeProposal group = new(
                        this,
                        locationIds[0],
                        sectionNumber,
                        lastModified,
                        circleRadius,
                        polygon,
                        AutoPolygonizeProposal.CreateRingViews(
                            polygon,
                            AutoPolygonizeProposal.ColorForLocation(locationIds[0]),
                            circleRadius,
                            downsample),
                        maskOverlay,
                        locationIds,
                        parentId,
                        overlapRound,
                        foreground,
                        background);
                    PublishProposal(group);
                }
                finally
                {
                    if (acquiredHold && holdImageId is ulong id)
                        cache.ReleaseBatchHold(id);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto polygonize overlap resubmit failed: {ex.Message}");
            }
            finally
            {
                lock (overlapResubmitsInFlight)
                    overlapResubmitsInFlight.Remove(inFlightKey);
            }
        }

        private void DropProposalsForSection(int sectionNumber)
        {
            long[] ids;
            lock (proposalLock)
            {
                ids = [.. proposals.Where(pair => pair.Value.SectionNumber == sectionNumber).Select(pair => pair.Key)];
            }

            foreach (long locationId in ids)
                RemoveProposal(locationId);
        }

        /// <summary>Drops GPU mask textures while keeping the outline proposals.</summary>
        private void DisposeAllMaskOverlays()
        {
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in DistinctProposalsUnlocked())
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
                        ForgetLocation(loc.ID);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                int count = Math.Max(e.OldItems?.Count ?? 0, e.NewItems?.Count ?? 0);
                for (int i = 0; i < count; i++)
                {
                    LocationObj? old = e.OldItems is not null && i < e.OldItems.Count ? e.OldItems[i] as LocationObj : null;
                    LocationObj? loc = e.NewItems is not null && i < e.NewItems.Count ? e.NewItems[i] as LocationObj : null;
                    if (loc is null)
                        continue;

                    if (loc.TypeCode != LocationType.CIRCLE)
                    {
                        bool inGroupedOverlay;
                        lock (proposalLock)
                        {
                            inGroupedOverlay = proposals.TryGetValue(loc.ID, out AutoPolygonizeProposal grouped)
                                && grouped.LocationIds.Count > 1;
                        }

                        if (!inGroupedOverlay)
                            ForgetLocation(loc.ID);
                        continue;
                    }

                    cache.ReplaceLocation(old, loc);
                }
            }
        }

        /// <summary>Live world bounds, or <see cref="lastViewBounds"/> if Scene is not ready (startup/teardown).</summary>
        private Rectangle GetCurrentViewportBounds()
        {
            if (parent.Scene is not null)
                return parent.Scene.VisibleWorldBounds;

            return lastViewBounds;
        }
    }
}
