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
        /// timer. A camera move cancels the upload and any in-flight SegmentTiles
        /// queue, then the next idle batch orders circles from the new view center.
        /// Section changes cancel both phases. An interactive
        /// <see cref="SegmentationCommand"/> cancels proposal work but
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

        /// <summary>Cancelled on camera or section change. Stops capture and upload only.</summary>
        private CancellationTokenSource? uploadCts;

        /// <summary>
        /// Cancelled when the view moves, the section changes, this controller is disposed,
        /// or a <see cref="SegmentationCommand"/> starts. Stops the circle queue and the in-flight RPC.
        /// </summary>
        private CancellationTokenSource processCts = new();
        private SegmentationViewportSession? uploadSession;
        private readonly List<ProcessBatch> processBatches = [];
        private readonly object lifecycleLock = new();
        private Rectangle lastViewBounds;
        /// <summary>
        /// Last submitted tile pyramid level from <see cref="SegmentationViewportSession.ResolveTileDownsample"/>.
        /// A change in that level drops stale proposals and re-queues. The current ceiling is 1.
        /// </summary>
        private int lastResolvedTileDownsample;
        private AutoPolygonizeProposal? hoveredProposal;

        /// <summary>
        /// <see cref="Stopwatch.GetTimestamp"/> of the last <see cref="RemoveProposalsThatAreNoLongerCircles"/> pass.
        /// UI thread only (Draw and hit-testing).
        /// </summary>
        private long lastStaleScanTimestamp;
        private readonly Dictionary<string, OverlapGroupJob> overlapResubmitsInFlight = [];
        private long requestTicketCounter;

        /// <summary>
        /// Next request ticket. Taken when a segmentation request starts, so a larger ticket always
        /// means a request the user asked for later, whatever order the answers arrive in.
        /// </summary>
        private long NextRequestTicket() => Interlocked.Increment(ref requestTicketCounter);

        /// <summary>
        /// One grouped overlap resubmit that is running. Starting a group that contains this job's
        /// locations cancels it so a smaller, older answer cannot arrive after the larger one.
        /// Owns <see cref="Cancellation"/> until <see cref="ResubmitOverlapGroupAsync"/> disposes it.
        /// </summary>
        private sealed class OverlapGroupJob(HashSet<long> locationIds, long ticket)
        {
            public HashSet<long> LocationIds { get; } = locationIds;

            /// <summary>Request ticket taken when the group was started; see <see cref="RequestSupersession"/>.</summary>
            public long Ticket { get; } = ticket;

            public CancellationTokenSource Cancellation { get; } = new();
        }

        /// <summary>
        /// One uploaded viewport plus the per-response polygonize tasks that still
        /// own that server image. Survives camera moves after upload.
        /// </summary>
        private sealed class ProcessBatch(SegmentationViewportSession session)
        {
            private int finishStarted;

            public SegmentationViewportSession Session { get; } = session;
            public List<Task> ResponseTasks { get; } = [];

            /// <summary>True for the first caller. Camera-move cancel and batch finally both finish the batch.</summary>
            public bool TryBeginFinish() => Interlocked.Exchange(ref finishStarted, 1) == 0;
        }

        public AutoCirclePolygonizeController(SectionViewerControl parent, Action requestAnnotationLoad)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.requestAnnotationLoad = requestAnnotationLoad ?? throw new ArgumentNullException(nameof(requestAnnotationLoad));
            viewportImageLease = new SharedViewportImageLease(cache);
            // Image releases stay subscribed even while disabled: a SegmentationCommand holds images in this
            // cache and its release must still delete the server image. Geometry and forget events only
            // matter while enabled, so SetEnabled/Stop attach and detach them.
            cache.ImageLeaseReleased += OnImageLeaseReleased;
            cache.ImageLeaseReleased += viewportImageLease.Forget;
        }

        /// <summary>Cache that owns SAM2 image holds. Segmentation commands take and release holds here.</summary>
        internal AutoPolygonizeCache ImageCache => cache;

        /// <summary>Current-view upload shared with <see cref="SegmentationCommand"/>.</summary>
        internal SharedViewportImageLease ViewportImages => viewportImageLease;

        public bool IsEnabled => enabled;

        /// <summary>
        /// True while an idle batch holds <c>batchRunning</c>. Status chips read this for AutoPoly Busy.
        /// </summary>
        internal bool IsBusy => Interlocked.CompareExchange(ref batchRunning, 0, 0) != 0;

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
            SegmentationDiag.Log($"AutoPolygonize.SetEnabled={value} svc={Global.IsSegmentationServiceAvailable}");
            if (enabled)
            {
                Store.Locations.OnCollectionChanged += OnLocationsChanged;
                cache.GeometryInvalidated -= OnCacheGeometryInvalidated;
                cache.GeometryInvalidated += OnCacheGeometryInvalidated;
                cache.LocationForgotten -= OnCacheLocationForgotten;
                cache.LocationForgotten += OnCacheLocationForgotten;
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
                lastResolvedTileDownsample = SegmentationViewportSession.ResolveTileDownsample(GetCurrentDownsample());
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
            cache.GeometryInvalidated -= OnCacheGeometryInvalidated;
            cache.LocationForgotten -= OnCacheLocationForgotten;
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
        /// Restarts settle when the view moved or the resolved tile pyramid level changed.
        /// Tile results depend on the submitted level, not on the viewport rectangle, so the
        /// in-flight SegmentTiles call, finished proposals and the completion cache are only
        /// discarded when the model's profile says the move made them stale
        /// (<see cref="SegmentationCameraPolicy"/>): any view move for a full-viewport model, a changed
        /// level for multi-resolution tiles. For single-resolution tiles that never happens, so a pan or zoom only stops the queue
        /// after the current circle; the next idle batch orders the rest from the new view center.
        /// </summary>
        public void OnCameraChanged()
        {
            if (!enabled)
                return;

            Rectangle current = GetCurrentViewportBounds();
            int resolvedTile = SegmentationViewportSession.ResolveTileDownsample(GetCurrentDownsample());
            bool boundsMoved = !SegmentationViewportSession.AreViewportBoundsSimilar(lastViewBounds, current);
            SegmentationModelProfile profile = SegmentationViewportSession.ModelProfile;
            bool tileLevelChanged = SegmentationCameraPolicy.TileLevelChanged(
                profile, lastResolvedTileDownsample, resolvedTile);
            if (!boundsMoved && !tileLevelChanged)
                return;

            SegmentationCameraPolicy.CameraMoveResponse response = SegmentationCameraPolicy.OnCameraMoved(
                profile, boundsMoved, tileLevelChanged);

            lastViewBounds = current;
            lastCameraChangeUtc = DateTime.UtcNow;
            if (response.ReorderQueue)
                Interlocked.Increment(ref cameraGeneration);
            if (response.DropFinishedResults)
            {
                DropProposalsForTileDownsampleChange();
                cache.InvalidateCompletionsAtOtherTileDownsample(resolvedTile);
            }

            lastResolvedTileDownsample = resolvedTile;
            viewportImageLease.ForgetIfViewMoved(current, GetCurrentDownsample());
            CancelUploadPhase();
            if (response.CancelInFlightSegmentation)
                CancelProcessPhase();
            CancelConfirmTimer();
            RestartIdleTimer();
        }

        /// <summary>
        /// Removes on-screen proposal rings when the submitted tile level changes so a
        /// zoom out past DS=1 (or zoom in back to DS=1) does not leave the old masks.
        /// </summary>
        private void DropProposalsForTileDownsampleChange()
        {
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in proposals.Values)
                    proposal.DisposeMaskOverlay();

                proposals.Clear();
            }

            hoveredProposal = null;
            parent.Invalidate();
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
        /// A SAM2 request setting (mask threshold, mask_input) changed, so every cached answer was
        /// made with the old value. Cancels in-flight work, forgets which circles were already
        /// segmented, and restarts the idle settle so the current view is requested again.
        /// Existing outlines stay up until the new batch replaces them, and the uploaded images
        /// stay valid, so nothing is re-uploaded.
        /// </summary>
        public void OnSegmentationRequestSettingsChanged()
        {
            CancelProcessPhase();
            CancelUploadPhase();
            cache.Clear();
            if (enabled)
                RestartIdleTimer();
            else
                StopSettleTimers();
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
            Interlocked.Increment(ref cameraGeneration);
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
        /// Hover and double-click call this directly. Single-click <c>ObjectAtPosition</c>
        /// prefers annotations so the circle under a ring stays editable.
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

        /// <summary>Highlights the proposal under the cursor and invalidates when the hit changes.
        /// Also dims the source circle annotation(s) via <see cref="IsHoveredSourceLocation"/>.
        /// </summary>
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
        /// True when <paramref name="locationId"/> belongs to the hovered proposal ring.
        /// Called from <see cref="View.LocationCircleView.Draw"/> so the source circle is
        /// drawn at half opacity while the ring is highlighted.
        /// </summary>
        internal bool IsHoveredSourceLocation(long locationId)
        {
            if (hoveredProposal is null)
                return false;

            IReadOnlyList<long> ids = hoveredProposal.LocationIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == locationId)
                    return true;
            }

            return false;
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
            // #region agent log
            SegmentationDiag.Log(
                $"DIAG H14 Accept ids=[{string.Join(",", proposal.LocationIds)}] members={members.Count} survivor={survivorId} " +
                $"carvedNull={toApply is null} proposalVerts={proposal.Polygon?.TotalUniqueVertices}");
            // #endregion
            if (toApply is null)
            {
                RemoveProposal(proposal.LocationId);
                parent.Invalidate();
                return;
            }

            toApply = AutoPolygonizeSelection.SimplifyForCreatedShape(toApply, parent.Downsample);
            bool dbgApplied = LocationShapeUpdate.ApplyVolumePolygon(survivor, toApply, parent);
            // #region agent log
            SegmentationDiag.Log($"DIAG H14 Accept ApplyVolumePolygon survivor={survivorId} applied={dbgApplied}");
            // #endregion
            if (!dbgApplied)
            {
                // The proposal stays so the user can try again or reject it; silence read as a dead double-click.
                parent.ShowTransientStatus("Could not save the auto-segment outline for this annotation.");
                return;
            }

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

            Viking.UI.State.MainThreadDispatcher?.BeginInvoke(new Action(ArmConfirmIfStillQuiet));
        }

        /// <summary>Confirm-timer callback. Marshals to the UI thread to compare the armed snapshot to the live view.</summary>
        private void OnConfirmElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!enabled)
                return;

            Viking.UI.State.MainThreadDispatcher?.BeginInvoke(new Action(RunBatchIfViewStillMatches));
        }

        /// <summary>
        /// Records live bounds/downsample, then starts the 1s confirm. If a camera
        /// event was processed after this method was queued, restarts idle instead.
        /// When the camera is coarser than <see cref="Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>,
        /// stops settling until a camera move or <see cref="RequestIdlePass"/> (preference change) re-arms.
        /// </summary>
        private void ArmConfirmIfStillQuiet()
        {
            if (!enabled)
                return;

            global::WebAnnotation.UI.AnnotationStatusChips.Refresh();
            if (!IsWithinAutoSegmentDownsample())
            {
                SegmentationDiag.Log(
                    $"ArmConfirm skip: downsample out of range ds={GetCurrentDownsample()} " +
                    $"max={Global.AnnotationSettings.AutoPolygonizeMaxDownsample}");
                return;
            }

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

            ObserveFaults(RunBatchAsync(), "batch");
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
        /// Equality still runs. The preference cannot exceed DS 4, so DS 5 never starts.
        /// A lower saved preference (default 2) still blocks the coarser views.
        /// </summary>
        private bool IsWithinAutoSegmentDownsample()
        {
            return GetCurrentDownsample() <= Global.AnnotationSettings.AutoPolygonizeMaxDownsample;
        }

        /// <summary>
        /// True when auto-polygonize is on but the camera is too coarse to start a batch. The idle
        /// settle exits silently in that state, so the status chip needs this to say why nothing is sent.
        /// </summary>
        internal bool IsPausedByZoom => enabled && parent.Camera is not null && !IsWithinAutoSegmentDownsample();

        /// <summary>
        /// Restarts the idle wait when auto-segment is on. Used after the max-downsample preference changes
        /// so a view that is now allowed can run without a camera nudge. Safe to call repeatedly; a one-shot
        /// idle that previously exited because the camera was too coarse is re-armed here.
        /// </summary>
        public void RequestIdlePass()
        {
            if (!enabled)
            {
                SegmentationDiag.Log("RequestIdlePass skip: auto-polygonize disabled");
                return;
            }

            SegmentationDiag.Log(
                $"RequestIdlePass ds={GetCurrentDownsample()} " +
                $"max={Global.AnnotationSettings.AutoPolygonizeMaxDownsample} " +
                $"inRange={IsWithinAutoSegmentDownsample()}");
            RestartIdleTimer();
        }

        private int batchRunning;

        /// <summary>Set when a newer view asked to run while a batch was still winding down.</summary>
        private int rerunAfterBatch;

        /// <summary>Bumped on camera and section changes so a batch does not start SegmentTiles for a view it already left.</summary>
        private int cameraGeneration;

        /// <summary>
        /// One capture/upload plus sequential SegmentTiles calls, nearest circle first.
        /// A camera move cancels this batch. If the confirm timer fires before the
        /// cancelled call returns, the finish path restarts the idle settle so the
        /// new view is not dropped.
        /// </summary>
        private async Task RunBatchAsync()
        {
            if (Interlocked.CompareExchange(ref batchRunning, 1, 0) != 0)
            {
                Interlocked.Exchange(ref rerunAfterBatch, 1);
                SegmentationDiag.Log("RunBatch skip: already running; queued rerun");
                return;
            }

            global::WebAnnotation.UI.AnnotationStatusChips.Refresh();
            try
            {
                await RunBatchBodyAsync().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref batchRunning, 0);
                global::WebAnnotation.UI.AnnotationStatusChips.Refresh();
                if (Interlocked.Exchange(ref rerunAfterBatch, 0) == 1 && enabled)
                    RestartIdleTimer();
            }
        }

        /// <summary>Body of an auto-polygonize batch; only one runs at a time via <see cref="batchRunning"/>.</summary>
        private async Task RunBatchBodyAsync()
        {
            SegmentationDiag.Log(
                $"RunBatch enter enabled={enabled} svc={Global.IsSegmentationServiceAvailable} " +
                $"sceneNull={parent.Scene is null} sectionNull={parent.Section is null}");
            if (!enabled || !Global.IsSegmentationServiceAvailable || parent.Scene is null || parent.Section is null)
                return;

            if (!IsWithinAutoSegmentDownsample())
            {
                SegmentationDiag.Log($"RunBatch skip: downsample out of range ds={parent.Camera?.Downsample}");
                return;
            }

            if (parent.CurrentCommand is SegmentationCommand)
            {
                SegmentationDiag.Log("RunBatch skip: SegmentationCommand active");
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

                AnnotationWaitResult annotationWait = await WaitForAnnotationsLoadedAsync(uploadToken).ConfigureAwait(false);
                SegmentationDiag.Log($"RunBatch annotationWait={annotationWait} cancelled={uploadToken.IsCancellationRequested}");
                if (annotationWait == AnnotationWaitResult.TimedOut)
                {
                    // Avoid marks come from the loaded annotations. Segmenting against a partial set would cache
                    // circles as done with missing avoid points, so retry once for this view, then give up.
                    int viewAtTimeout = Volatile.Read(ref cameraGeneration);
                    if (Interlocked.Exchange(ref annotationTimeoutRetryGeneration, viewAtTimeout) != viewAtTimeout && enabled)
                        RestartIdleTimer();
                    return;
                }

                if (annotationWait != AnnotationWaitResult.Complete)
                    return;

                long annotationLoadMs = stepTimer.ElapsedMilliseconds;
                if (uploadToken.IsCancellationRequested || !enabled || parent.CurrentCommand is SegmentationCommand)
                {
                    SegmentationDiag.Log("RunBatch abort after annotations (cancel/disabled/command)");
                    return;
                }

                stepTimer.Restart();
                localUploadSession = new SegmentationViewportSession(parent);
                if (!localUploadSession.TryInitializeClient())
                {
                    SegmentationDiag.Log("RunBatch abort: TryInitializeClient failed");
                    return;
                }

                lock (lifecycleLock)
                {
                    uploadSession = localUploadSession;
                }

                localUploadSession.ViewportBounds = localUploadSession.GetCurrentViewportBounds();
                Rectangle viewBounds = localUploadSession.ViewportBounds;
                int viewGeneration = Volatile.Read(ref cameraGeneration);
                Rectangle inset = AutoPolygonizeSelection.InsetBounds(viewBounds);
                List<LocationObj> candidates = CollectEligibleCircles(viewBounds, inset);
                SegmentationDiag.Log($"RunBatch candidates={candidates.Count} bounds={viewBounds}");
                if (candidates.Count == 0)
                {
                    ScheduleSavedSiblingOverlapScan();
                    return;
                }

                long candidateCollectionMs = stepTimer.ElapsedMilliseconds;
                IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
                int sectionNumber = parent.Section.Number;
                double downsample = parent.Camera.Downsample;
                double simplifyTolerance = AutoPolygonizeSelection.CreatedShapeSimplifyWorld(parent.Downsample);

                stepTimer.Restart();
                SegmentationDiag.Log("RunBatch CaptureSharedViewportAsync begin");
                AutoPolygonizeUploadContext? uploadContext = await CaptureSharedViewportAsync(
                    localUploadSession,
                    viewBounds,
                    downsample,
                    uploadToken).ConfigureAwait(false);
                SegmentationDiag.Log(
                    $"RunBatch CaptureSharedViewportAsync done null={uploadContext is null} " +
                    $"imageId={uploadContext?.ImageId} wh={uploadContext?.Width}x{uploadContext?.Height}");
                if (uploadContext is null)
                    return;

                if (uploadToken.IsCancellationRequested || !enabled || parent.CurrentCommand is SegmentationCommand)
                    return;

                if (uploadContext.Value.ImageId != 0)
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

                // Avoid marks come from the other annotations in view. The view is fixed for this batch (a camera
                // move cancels it), so one spatial query serves every circle instead of one per circle.
                List<LocationObj> visibleForPrompts = [.. CollectVisibleLocationObjs(viewBounds)];
                foreach (LocationObj circle in candidates)
                {
                    if (processToken.IsCancellationRequested ||
                        !enabled ||
                        Volatile.Read(ref cameraGeneration) != viewGeneration)
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
                    if (!CircleSegmentationPrompts.TryCreateStartingPrompt(
                            CircleSegmentationPrompts.ToVolumePoints(
                                CircleSegmentationPrompts.CreateMosaicRadiusPoints(new Circle(circle.Position, circle.Radius)),
                                transform),
                            out CircleSegmentationPrompts.StartingPrompt startingPrompt))
                    {
                        continue;
                    }

                    IReadOnlyList<Vector2> foreground = startingPrompt.Points;
                    IReadOnlyList<Rectangle> startingBoxes = [startingPrompt.Box];
                    IReadOnlyList<Vector2> keepPoints = [startingPrompt.Center];

                    IReadOnlyList<Vector2> background = CircleSegmentationPrompts.CreateOtherStructureBackgroundVolumePoints(
                        visibleForPrompts,
                        transform,
                        foreground,
                        Global.AnnotationSettings.SegmentationPointRadius * downsample,
                        [circle.ID],
                        circle.ParentID);
                    SegmentationDiag.Log(CircleSegmentationPrompts.DescribePrompts(
                        "auto-circle-batch", [circle.ID], circle.ParentID, visibleForPrompts, foreground, background));
                    long promptMs = proposalTimer.ElapsedMilliseconds;

                    long requestTicket = NextRequestTicket();
                    Stopwatch segmentTimer = Stopwatch.StartNew();
                    var response = await localUploadSession.SegmentAsync(
                        foreground,
                        background,
                        processToken,
                        startingBoxes,
                        requestId: (ulong)requestTicket).ConfigureAwait(false);
                    long segmentMs = segmentTimer.ElapsedMilliseconds;
                    if (response is null)
                        continue;

                    try
                    {
                        // The live level and viewport are read here, before the response is handed to a
                        // worker, so processing never blocks a pool thread on the UI dispatcher.
                        LiveViewCheck liveCheck = await CaptureLiveViewCheckAsync(processBatch.Session, response).ConfigureAwait(false);
                        Task responseTask = ProcessResponseAsync(
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
                            liveCheck,
                            processToken,
                            requestTicket,
                            keepPoints);
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
        /// The live camera at the moment a response arrived, read on the UI thread by the caller so
        /// <see cref="ProcessResponseAsync"/> never blocks a worker thread on the dispatcher.
        /// </summary>
        private readonly struct LiveViewCheck
        {
            public LiveViewCheck(int resultLevel, int liveLevel, bool viewportUnchanged)
            {
                ResultLevel = resultLevel;
                LiveLevel = liveLevel;
                ViewportUnchanged = viewportUnchanged;
            }

            /// <summary>Tile level the response was requested at (fixed per response, not the session's current level).</summary>
            public int ResultLevel { get; }

            /// <summary>Tile level the live camera would submit now.</summary>
            public int LiveLevel { get; }

            /// <summary>False when a viewport-dependent model's result no longer matches the camera.</summary>
            public bool ViewportUnchanged { get; }
        }

        /// <summary>
        /// Reads the live tile level and viewport through the dispatcher without blocking. Await it before handing
        /// the response to <see cref="ProcessResponseAsync"/>.
        /// </summary>
        private static async Task<LiveViewCheck> CaptureLiveViewCheckAsync(
            SegmentationViewportSession session,
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentationResponse response)
        {
            SegmentationModelProfile profile = SegmentationViewportSession.ModelProfile;
            int resultLevel = session.DownsampleFor(response);
            int liveLevel = profile.TileLevelCanChange
                ? await session.GetLiveTileDownsampleAsync().ConfigureAwait(false)
                : resultLevel;

            bool viewportUnchanged = true;
            if (profile.ResultDependsOnViewport)
            {
                Rectangle liveBounds = await session.GetLiveViewportBoundsAsync().ConfigureAwait(false);
                viewportUnchanged = SegmentationViewportSession.AreViewportBoundsSimilar(session.ViewportBounds, liveBounds);
            }

            return new LiveViewCheck(resultLevel, liveLevel, viewportUnchanged);
        }

        /// <summary>
        /// Turns one SegmentImage response into a published proposal. The mask is polygonized and simplified on a
        /// worker (pure CPU work); everything that touches the location store, colors, the cache or the proposal
        /// list then runs as one UI-thread step, so no store access races a commit or a property change.
        /// Drops the result without caching if the section changed, or (multi-resolution only) the camera now
        /// resolves to a different tile level (<paramref name="liveCheck"/>), so a level change can retry.
        /// A pan or zoom inside one level keeps the result. An empty union is a LastModified skip (no overlay)
        /// so the next idle batch does not SegmentImage the same circle again. GPU mask textures are created
        /// later on the UI thread in <see cref="PublishProposalOnUiThread"/>.
        /// </summary>
        /// <param name="liveCheck">Null skips the level check (single-circle refresh, which segments against the live view).</param>
        private async Task ProcessResponseAsync(
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
            LiveViewCheck? liveCheck,
            CancellationToken processToken,
            long requestTicket,
            IReadOnlyList<Vector2>? extraKeepPoints = null)
        {
            if (processToken.IsCancellationRequested || !enabled || !cache.IsGenerationCurrent(circle.ID, generation))
                return;

            if (liveCheck is LiveViewCheck check &&
                !SegmentationCameraPolicy.IsResultStillValid(
                    SegmentationViewportSession.ModelProfile,
                    check.ResultLevel,
                    check.LiveLevel,
                    check.ViewportUnchanged))
            {
                Debug.WriteLine(
                    $"[SegmentationProfile] Auto batch={batchId} location={circle.ID} dropped: view or tile level changed before publish");
                return;
            }

            Stopwatch polygonTimer = Stopwatch.StartNew();
            (Polygon? Polygon, AutoPolygonizeMaskOverlay? Mask, int VerticesBeforeSimplify) processed = await Task.Run(() =>
            {
                // The clicks sit near the circle's edge, where a mask can break into pieces; the circle's
                // center is inside the object by construction, so it keeps the main piece.
                IReadOnlyList<Vector2> keepPoints = extraKeepPoints is { Count: > 0 }
                    ? [.. foreground, .. extraKeepPoints]
                    : foreground;
                IReadOnlyList<Polygon> polygons = session.CreatePolygonsFromResponse(
                    response,
                    preserveHolesContainingWorldPoints: background,
                    keepComponentsContainingWorldPoints: keepPoints);
                Polygon? first = polygons.FirstOrDefault();
                if (first is null)
                    return ((Polygon?)null, (AutoPolygonizeMaskOverlay?)null, 0);

                AutoPolygonizeMaskOverlay? mask = AutoPolygonizeMaskOverlay.TryCreate(session, response);
                int verticesBefore = first.TotalUniqueVertices;
                return (AutoPolygonizeSelection.SimplifyProposal(first, simplifyTolerance), mask, verticesBefore);
            }).ConfigureAwait(false);
            long polygonMs = polygonTimer.ElapsedMilliseconds;

            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null)
                return;

            await dispatcher.InvokeAsync(() =>
            {
                if (processToken.IsCancellationRequested ||
                    !enabled ||
                    !IsStillCircle(circle.ID) ||
                    !cache.IsGenerationCurrent(circle.ID, generation) ||
                    parent.Section is null ||
                    parent.Section.Number != sectionNumber)
                {
                    return;
                }

                if (processed.Polygon is null)
                {
                    RememberEmptyMask(circle, sectionNumber, session, downsample);
                    return;
                }

                Stopwatch renderPreparationTimer = Stopwatch.StartNew();
                Polygon? carved = CarveAgainstExistingPolygons(
                    processed.Polygon,
                    [circle.ID],
                    circle.VolumePosition,
                    circle.ParentID);
                if (carved is null)
                {
                    RememberEmptyMask(circle, sectionNumber, session, downsample);
                    return;
                }

                AutoPolygonizeProposal proposal = new(
                    this,
                    circle.ID,
                    sectionNumber,
                    circle.LastModified,
                    circle.Radius,
                    carved,
                    AutoPolygonizeProposal.CreateRingViews(
                        carved,
                        AutoPolygonizeProposal.ColorForLocation(circle.ID),
                        circle.Radius,
                        downsample),
                    processed.Mask,
                    locationIds: [circle.ID],
                    parentId: circle.ParentID,
                    foregroundPrompts: foreground,
                    backgroundPrompts: background)
                {
                    RequestTicket = requestTicket
                };
                long renderPreparationMs = renderPreparationTimer.ElapsedMilliseconds;

                // Remembering is part of publishing: a proposal dropped by supersession or an invalid member must
                // not leave the cache saying this circle is done with nothing on screen.
                bool published = PublishProposalOnUiThread(
                    proposal,
                    () => cache.RememberProposal(
                        circle.ID,
                        sectionNumber,
                        circle.LastModified,
                        circle.TypeCode,
                        Store.Locations.GetObjectByID(circle.ID, false),
                        SharedViewportImageLease.TryCreateContext(session),
                        downsample));
                if (!published)
                    return;

                Debug.WriteLine(
                    $"[SegmentationProfile] Auto batch={batchId} location={circle.ID} ready-to-draw " +
                    $"prompts={promptMs}ms segmentRpc={segmentMs}ms polygonize={polygonMs}ms " +
                    $"simplifyAndViews={renderPreparationMs}ms vertices={processed.VerticesBeforeSimplify}->{carved.TotalUniqueVertices} " +
                    $"proposal={proposalTimer.ElapsedMilliseconds}ms " +
                    $"batchElapsed={batchTimer.ElapsedMilliseconds}ms foreground={foreground.Count} background={background.Count}");
            }).Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Queues <see cref="PublishProposalOnUiThread"/> on the UI dispatcher. For callers that are not already
        /// on the UI thread and do not need to know whether the proposal was accepted.
        /// </summary>
        private void PublishProposal(AutoPolygonizeProposal proposal, Action? onPublished = null)
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null)
                return;

            dispatcher.BeginInvoke(new Action(() => PublishProposalOnUiThread(proposal, onPublished)));
        }

        /// <summary>
        /// UI-thread insert into <see cref="proposals"/>. Creates the optional mask
        /// overlay here because Texture2D must be allocated on the graphics thread.
        /// Returns false, after disposing the overlay, when auto-segment is off, a member is no longer valid, or
        /// a newer request already published for the same locations.
        /// <paramref name="onPublished"/> runs once the proposal is in the list and before overlap resubmit can
        /// start, so cache bookkeeping is never visible without the overlay and never recorded for a dropped one.
        /// </summary>
        private bool PublishProposalOnUiThread(AutoPolygonizeProposal proposal, Action? onPublished = null)
        {
            if (!enabled || !AreProposalMembersValid(proposal))
            {
                proposal.DisposeMaskOverlay();
                return false;
            }

            lock (proposalLock)
            {
                // Requests are fire-and-forget, so completion order is not start order. An answer to an
                // older request must not replace a published answer to a newer one for the same locations.
                if (RequestSupersession.IsSuperseded(
                    proposal.RequestTicket,
                    proposal.LocationIds.ToArray(),
                    DistinctProposalsUnlocked().Select(item =>
                        (item.RequestTicket, (IReadOnlyCollection<long>)item.LocationIds))))
                {
                    SegmentationDiag.Log(
                        $"publish dropped: ticket={proposal.RequestTicket} ids=[{string.Join(",", proposal.LocationIds)}] " +
                        "already superseded by a newer request");
                    proposal.DisposeMaskOverlay();
                    return false;
                }

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

            onPublished?.Invoke();
            parent.Invalidate();
            TryBeginOverlapResubmit(proposal);
            return true;
        }

        /// <summary>
        /// Polls region queries so circles exist before candidate collection.
        /// </summary>
        /// <returns>
        /// <see cref="AnnotationWaitResult.Complete"/> when the region queries finished,
        /// <see cref="AnnotationWaitResult.TimedOut"/> when <see cref="RegionWaitTimeoutMs"/> passed first, and
        /// <see cref="AnnotationWaitResult.Cancelled"/> when cancelled or the view is gone.
        /// </returns>
        private async Task<AnnotationWaitResult> WaitForAnnotationsLoadedAsync(CancellationToken token)
        {
            if (parent.Scene is null || parent.Section is null)
                return AnnotationWaitResult.Cancelled;

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
                    return AnnotationWaitResult.Complete;
                }

                try
                {
                    await Task.Delay(RegionPollMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return AnnotationWaitResult.Cancelled;
                }
            }

            return token.IsCancellationRequested ? AnnotationWaitResult.Cancelled : AnnotationWaitResult.TimedOut;
        }

        /// <summary>Outcome of <see cref="WaitForAnnotationsLoadedAsync"/>.</summary>
        private enum AnnotationWaitResult
        {
            Complete,
            TimedOut,
            Cancelled
        }

        /// <summary>
        /// View generation for which <see cref="RunBatchBodyAsync"/> already retried after an annotation-load
        /// timeout. A second timeout on the same view gives up instead of looping on a store that never finishes.
        /// </summary>
        private int annotationTimeoutRetryGeneration = -1;

        /// <summary>
        /// Circles whose center is at least 5% from each edge, whose disk is fully
        /// on screen, large enough, and not already proposed or dismissed for this LastModified.
        /// Ordered nearest-to-farthest from <paramref name="viewBounds"/> center.
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
        /// Cancels in-flight SegmentTiles and per-response CPU work.
        /// Called when the view moves, the section changes, this controller stops, or Segment starts.
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
                ObserveFaults(FinishProcessBatchAsync(batch), "finish process batch");
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
            if (!batch.TryBeginFinish())
                return;

            try
            {
                Task[] pending = [.. batch.ResponseTasks];
                if (pending.Length > 0)
                    await Task.WhenAll(pending).ConfigureAwait(false);
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
            // Draw and hover call this every frame; each pass walks every proposal and reads the store. A stale
            // ring surviving a few frames after a convert is harmless, a per-frame LINQ pass is not.
            long now = Stopwatch.GetTimestamp();
            if (now - lastStaleScanTimestamp < Stopwatch.Frequency / 4)
                return;

            lastStaleScanTimestamp = now;
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
            refreshQueue ??= new LocationRefreshQueue(RunForLocationAsync, () => enabled, maxConcurrent: 2);
            ObserveFaults(refreshQueue.RefreshAsync(locationId, sectionNumber, generation), "single-ID refresh");
        }

        /// <summary>
        /// Runs single-circle refreshes one per location at a time, two overall. Dragging a circle fires one
        /// geometry change per frame; without this each one started its own viewport upload.
        /// </summary>
        private LocationRefreshQueue? refreshQueue;

        /// <summary>
        /// Logs the exception of a task nobody awaits. Every background step already catches and logs its own
        /// failures; this is the backstop so a faulted task is never silently unobserved.
        /// </summary>
        private static void ObserveFaults(Task task, string what)
        {
            _ = task.ContinueWith(
                completed => SegmentationDiag.Log(
                    $"AutoPolygonize background task failed ({what}): {completed.Exception?.GetBaseException().Message}"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void OnCacheLocationForgotten(long locationId)
        {
            RemoveProposal(locationId);
            if (enabled)
                parent.Invalidate();
        }

        private void OnImageLeaseReleased(ulong imageId)
        {
            ObserveFaults(DeleteLeasedImageAsync(imageId), "delete leased image");
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
                if (reuse && uploadContext.ImageId != 0)
                {
                    session.AdoptUploadedImage(
                        uploadContext.ImageId,
                        uploadContext.WorldBounds,
                        uploadContext.Width,
                        uploadContext.Height);
                    if (SharedViewportImageLease.CanReuse(uploadContext, viewBounds, downsample))
                        viewportImageLease.Publish(uploadContext);
                }
                else if (reuse)
                {
                    // Tiled mode: ImageId is 0. Refresh tiles on this session; do not Adopt/DeleteImage.
                    AutoPolygonizeUploadContext? uploaded = await CaptureSharedViewportAsync(
                        session,
                        viewBounds,
                        downsample,
                        processToken).ConfigureAwait(false);
                    if (uploaded is null)
                        return;
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

                    if (uploaded.Value.ImageId != 0)
                    {
                        cache.AcquireBatchHold(uploaded.Value.ImageId);
                        acquiredHold = true;
                        holdImageId = uploaded.Value.ImageId;
                    }
                }

                IVolumeToSectionTransform transform = parent.Section.ActiveSectionToVolumeTransform;
                if (!CircleSegmentationPrompts.TryCreateStartingPrompt(
                        CircleSegmentationPrompts.ToVolumePoints(
                            CircleSegmentationPrompts.CreateMosaicRadiusPoints(new Circle(circle.Position, circle.Radius)),
                            transform),
                        out CircleSegmentationPrompts.StartingPrompt startingPrompt))
                {
                    return;
                }

                IReadOnlyList<Vector2> foreground = startingPrompt.Points;
                IReadOnlyList<Rectangle> startingBoxes = [startingPrompt.Box];
                IReadOnlyList<Vector2> keepPoints = [startingPrompt.Center];

                List<LocationObj> visibleForPrompts = [.. CollectVisibleLocationObjs(viewBounds)];
                IReadOnlyList<Vector2> background = CircleSegmentationPrompts.CreateOtherStructureBackgroundVolumePoints(
                    visibleForPrompts,
                    transform,
                    foreground,
                    Global.AnnotationSettings.SegmentationPointRadius * downsample,
                    [circle.ID],
                    circle.ParentID);
                SegmentationDiag.Log(CircleSegmentationPrompts.DescribePrompts(
                    "auto-circle", [circle.ID], circle.ParentID, visibleForPrompts, foreground, background));

                Stopwatch proposalTimer = Stopwatch.StartNew();
                long requestTicket = NextRequestTicket();
                var response = await session.SegmentAsync(
                    foreground,
                    background,
                    processToken,
                    startingBoxes,
                    requestId: (ulong)requestTicket).ConfigureAwait(false);
                if (response is null)
                    return;

                downsample = GetCurrentDownsample();
                AutoPolygonizeUploadContext? afterSegment = SharedViewportImageLease.TryCreateContext(session);
                if (afterSegment is { ImageId: not 0 } next &&
                    holdImageId is ulong held &&
                    held != next.ImageId)
                {
                    cache.AcquireBatchHold(next.ImageId);
                    cache.ReleaseBatchHold(held);
                    holdImageId = next.ImageId;
                }

                int liveGeneration = cache.MarkPending(circle.ID, sectionNumber, circle, afterSegment);
                await ProcessResponseAsync(
                    session,
                    circle,
                    sectionNumber,
                    downsample,
                    AutoPolygonizeSelection.CreatedShapeSimplifyWorld(parent.Downsample),
                    foreground,
                    background,
                    response,
                    0,
                    0,
                    proposalTimer,
                    proposalTimer,
                    0,
                    liveGeneration,
                    liveCheck: null,
                    processToken,
                    requestTicket,
                    keepPoints).ConfigureAwait(false);
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
                SharedViewportImageLease.TryCreateContext(session),
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
            AutoPolygonizeUploadContext? upload = SharedViewportImageLease.TryCreateContext(session);
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

            if (ready.ImageId != 0 && session.CurrentImageId != ready.ImageId)
            {
                session.AdoptUploadedImage(
                    ready.ImageId,
                    ready.WorldBounds,
                    ready.Width,
                    ready.Height);
            }
            else if (ready.ImageId == 0 && !session.HasUploadedTiles)
            {
                session.ViewportBounds = viewBounds;
                if (!await session.UploadCurrentImageAsync(cancellationToken).ConfigureAwait(false))
                    return null;
                return SharedViewportImageLease.TryCreateContext(session);
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

            return SharedViewportImageLease.TryCreateContext(session);
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
            OverlapGroupJob job = new([.. locationIds], NextRequestTicket());
            lock (overlapResubmitsInFlight)
            {
                if (overlapResubmitsInFlight.ContainsKey(key))
                {
                    job.Cancellation.Dispose();
                    return;
                }

                foreach (OverlapGroupJob older in overlapResubmitsInFlight.Values)
                {
                    if (older.LocationIds.Count < job.LocationIds.Count && older.LocationIds.IsSubsetOf(job.LocationIds))
                    {
                        try
                        {
                            older.Cancellation.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    }
                }

                overlapResubmitsInFlight[key] = job;
            }

            ObserveFaults(ResubmitOverlapGroupAsync(members, locationIds, currentRound + 1, key, job), "overlap resubmit");
        }

        /// <summary>
        /// Visible saved POLYGON/CURVEPOLYGON fills of the same cell that overlap.
        /// Used when every sibling is already converted so no circle proposal exists.
        /// </summary>
        private void ScheduleSavedSiblingOverlapScan()
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null)
                TryBeginSavedSiblingOverlapResubmits();
            else
                dispatcher.BeginInvoke(new Action(TryBeginSavedSiblingOverlapResubmits));
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
            Polygon? carved = AutoPolygonizeSelection.SubtractOverlappingPolygons(proposed, existing, keepPoint);
            SegmentationDiag.Log(
                $"Carve exclude=[{string.Join(",", excludeLocationIds)}] keepPoint={keepPoint} " +
                $"proposed area={proposed.Area:F0} bbox={proposed.BoundingBox} vertices={proposed.TotalUniqueVertices}; " +
                $"overlapping existing={existing.Count} [{string.Join("; ", existing.Select(polygon => $"area={polygon.Area:F0} bbox={polygon.BoundingBox}"))}]; " +
                (carved is null
                    ? "result=null (existing annotations cover the proposal)"
                    : $"result area={carved.Area:F0} bbox={carved.BoundingBox} vertices={carved.TotalUniqueVertices}"));
            return carved;
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
            string inFlightKey,
            OverlapGroupJob job)
        {
            CancellationTokenSource? linkedCancellation = null;
            try
            {
                if (!enabled || parent.Section is null || parent.Scene is null)
                    return;

                if (!IsWithinAutoSegmentDownsample())
                    return;

                CancellationToken lifecycleToken;
                lock (lifecycleLock)
                {
                    lifecycleToken = processCts.Token;
                }

                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken, job.Cancellation.Token);
                CancellationToken processToken = linkedCancellation.Token;
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
                            upload.IsUsable &&
                            upload.ImageId != 0)
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

                        if (uploaded.Value.ImageId != 0)
                        {
                            cache.AcquireBatchHold(uploaded.Value.ImageId);
                            acquiredHold = true;
                            holdImageId = uploaded.Value.ImageId;
                        }
                    }

                    IReadOnlyList<Polygon> promptPolygons = CollectOverlapPromptPolygons(members, locationIds);
                    CircleSegmentationPrompts.GroupPrompt groupPrompt =
                        CircleSegmentationPrompts.CreateGroupPromptFromPolygons(promptPolygons);
                    IReadOnlyList<Vector2> foreground = groupPrompt.Foreground;
                    IReadOnlyList<Rectangle> foregroundBoxes = groupPrompt.Box is Rectangle groupBox ? [groupBox] : [];
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
                    List<LocationObj> visibleForPrompts = [.. CollectVisibleLocationObjs(viewBounds)];
                    IReadOnlyList<Vector2> background = CircleSegmentationPrompts.CreateOtherStructureBackgroundVolumePoints(
                            visibleForPrompts,
                            parent.Section.ActiveSectionToVolumeTransform,
                            foreground,
                            Global.AnnotationSettings.SegmentationPointRadius * downsample,
                            involved,
                            parentId);
                    SegmentationDiag.Log(CircleSegmentationPrompts.DescribePrompts(
                        "auto-overlap-group", involved, parentId, visibleForPrompts, foreground, background));
                    SegmentationDiag.Log($"auto-overlap-group boxes={foregroundBoxes.Count} clicks={foreground.Count}");

                    // Snapshot before the remask: OR keeps every original footprint even when the
                    // group SegmentAsync returns NO_MATCHING_MASK or a partial remask.
                    List<Polygon> originalPolygons = [.. promptPolygons];
                    List<AutoPolygonizeMaskOverlay?> originalMasks = [.. members.Select(member => member.MaskOverlay)];

                    var response = await session.SegmentAsync(
                        foreground,
                        background,
                        processToken,
                        foregroundBoxes,
                        (ulong)job.Ticket).ConfigureAwait(false);
                    if (processToken.IsCancellationRequested)
                        return;

                    if (!locationIds.All(IsGroupMemberValid))
                        return;

                    double simplifyTolerance = AutoPolygonizeSelection.CreatedShapeSimplifyWorld(parent.Downsample);
                    (Polygon? Remask, AutoPolygonizeMaskOverlay? RemaskOverlay) remask = (null, null);
                    if (response is not null)
                    {
                        remask = await Task.Run(() =>
                        {
                            IReadOnlyList<Polygon> polygons = session.CreatePolygonsFromResponse(
                                response,
                                preserveHolesContainingWorldPoints: background,
                                keepComponentsContainingWorldPoints: foreground);
                            Polygon? first = polygons.FirstOrDefault();
                            if (first is null)
                                return ((Polygon?)null, (AutoPolygonizeMaskOverlay?)null);

                            return (
                                AutoPolygonizeSelection.SimplifyProposal(first, simplifyTolerance),
                                AutoPolygonizeMaskOverlay.TryCreate(session, response));
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        SegmentationDiag.Log(
                            $"auto-overlap-group remask missing ids=[{string.Join(",", locationIds)}]; " +
                            "OR of original sibling masks will still publish");
                    }

                    Vector2 unionKeepPoint = originalPolygons.Count > 0
                        ? originalPolygons[0].Centroid
                        : foreground[0];

                    List<Polygon?> unionInputs = [.. originalPolygons];
                    if (remask.Remask is not null)
                        unionInputs.Add(remask.Remask);

                    Polygon? merged = await Task.Run(() =>
                        AutoPolygonizeSelection.UnionPolygons(unionInputs, unionKeepPoint)).ConfigureAwait(false);
                    if (merged is not null)
                        merged = AutoPolygonizeSelection.SimplifyProposal(merged, simplifyTolerance);

                    AutoPolygonizeMaskOverlay? mergedMask = AutoPolygonizeMaskOverlay.TryOr(
                        [.. originalMasks, remask.RemaskOverlay]);

                    SegmentationDiag.Log(
                        $"auto-overlap-group OR originals={originalPolygons.Count} remask={(remask.Remask is null ? "none" : $"area={remask.Remask.Area:F0}")} " +
                        $"merged={(merged is null ? "null" : $"area={merged.Area:F0}")} mask={(mergedMask is null ? "none" : $"{mergedMask.Width}x{mergedMask.Height}")}");

                    var dispatcher = Viking.UI.State.MainThreadDispatcher;
                    if (dispatcher is null)
                        return;

                    // Store reads, carving, colors, cache bookkeeping and publishing are one UI-thread step. It is
                    // awaited so the batch hold below is released only after the cache holds the image itself.
                    await dispatcher.InvokeAsync(() =>
                    {
                        if (processToken.IsCancellationRequested || !locationIds.All(IsGroupMemberValid))
                            return;

                        Polygon? polygon = merged;
                        if (polygon is not null)
                        {
                            LocationObj? keepLocation = Store.Locations.GetObjectByID(locationIds[0], false);
                            Vector2 keepPoint = keepLocation?.VolumePosition ?? unionKeepPoint;
                            polygon = CarveAgainstExistingPolygons(
                                polygon,
                                locationIds,
                                keepPoint,
                                parentId);
                        }

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
                            mergedMask,
                            locationIds,
                            parentId,
                            overlapRound,
                            foreground,
                            background)
                        {
                            RequestTicket = job.Ticket
                        };

                        PublishProposalOnUiThread(group, () =>
                        {
                            AutoPolygonizeUploadContext? uploadContext = SharedViewportImageLease.TryCreateContext(session);
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
                        });
                    }).Task.ConfigureAwait(false);
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
                linkedCancellation?.Dispose();
                job.Cancellation.Dispose();
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
