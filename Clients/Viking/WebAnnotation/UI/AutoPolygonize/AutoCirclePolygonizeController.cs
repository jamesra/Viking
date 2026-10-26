using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.DependencyInjection;
using Viking.Services.Grpc;
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
    /// A batch starts once the view has been quiet for 500 ms. The view snapshot taken when that window opens is
    /// compared with the live camera when it closes, so a UI hitch cannot pass the timer. Circles go out one at a
    /// time, nearest the cursor first (the view centre when the cursor is off the view). The next circle is picked
    /// again before each request. A camera move cancels the upload but never the queue: a result on the
    /// single-resolution tile grid does not depend on the camera (<see cref="SegmentationCameraPolicy"/>), so the queue
    /// keeps sending its circles that are still on screen, and the next batch collects the ones that came into view.
    /// Section changes cancel both phases. An interactive <see cref="SegmentationCommand"/> cancels proposal work
    /// but leaves an in-flight viewport upload so that command can adopt the same image.
    /// </summary>
    internal sealed class AutoCirclePolygonizeController
    {
        /// <summary>
        /// Wall-clock quiet time before a batch. The timer keeps running during UI hitches, so when it fires the
        /// live view is compared with the snapshot taken when it started.
        /// </summary>
        private const int IdleDebounceMs = 500;

        /// <summary>
        /// Requests a batch keeps open at once. The next circle is picked and sent as soon as a request goes out, so
        /// the server always has work waiting while others wait on tile round trips. Kept small so the cursor still
        /// decides most of the order.
        /// </summary>
        private const int MaxRequestsInFlight = 3;

        /// <summary>
        /// The batch prefetches the tiles within this many radii of each queued circle's centre, which covers what the
        /// server usually grows a circle's mask into. Tiles further out are still uploaded when the server asks.
        /// </summary>
        private const double PrefetchRadii = 4;

        private const int RegionPollMs = 150;
        private const int RegionWaitTimeoutMs = 10000;
        private const int TileWaitTimeoutMs = 30000;
        private static long profileBatchSequence;

        private readonly SectionViewerControl parent;
        private readonly Action requestAnnotationLoad;
        private readonly AutoPolygonizeCache cache = new();
        private readonly SharedViewportImageLease viewportImageLease = new();
        private readonly Dictionary<long, AutoPolygonizeProposal> proposals = [];
        private readonly object proposalLock = new();

        /// <summary>
        /// Proposals that Del hid, keyed by <see cref="HiddenKey"/>. A hidden ring is neither drawn nor hit-tested until its
        /// time runs out; it then fades back in before it can be picked.
        /// </summary>
        private readonly TemporarilyHiddenAnnotations hiddenProposals = new();

        private bool enabled;
        private System.Timers.Timer? idleTimer;

        /// <summary>Viewport captured when the idle timer started; compared with the live view when it fires.</summary>
        private Rectangle armedBounds;
        private double armedDownsample;
        private DateTime armedAtUtc;

        /// <summary>Updated on the UI thread. An idle callback aborts if this is newer than <see cref="armedAtUtc"/>.</summary>
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
        private AutoPolygonizeProposal? hoveredProposal;

        /// <summary>
        /// Proposal removed from the overlay while <see cref="BeginPaintMask"/> runs an interactive
        /// <see cref="SegmentationPaintCommand"/>. Escape restores it; accept clears it.
        /// </summary>
        private AutoPolygonizeProposal? parkedForAddPoints;

        /// <summary>
        /// <see cref="Stopwatch.GetTimestamp"/> of the last <see cref="RemoveProposalsThatAreNoLongerCircles"/> pass.
        /// UI thread only (Draw and hit-testing).
        /// </summary>
        private long lastStaleScanTimestamp;
        private readonly Dictionary<string, OverlapGroupJob> overlapResubmitsInFlight = [];
        private long requestTicketCounter;

        /// <summary>
        /// Circles of the same structure close to a finished result, on screen or not, that may be part of the same
        /// cell. They go out before everything else (see <see cref="QueueMergeCandidates"/>). Persists across batches:
        /// a camera move that cancels a batch must not lose them.
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentQueue<long> mergeCandidateQueue = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, byte> mergeCandidateIds = new();

        /// <summary>When each structure's locations were last fetched from the server for the merge check.</summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, DateTime> structureFetchedUtc = new();

        /// <summary>How long fetched structure locations are trusted before the merge check asks the server again.</summary>
        private static readonly TimeSpan StructureFetchLifetime = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Bumped when edge cleanup or hole dropping is applied again, so a slower retrace
        /// does not paint over a newer radius.
        /// </summary>
        private int maskRefreshGeneration;

        /// <summary>
        /// Next request ticket. Taken when a segmentation request starts, so a larger ticket always
        /// means a request the user asked for later, whatever order the answers arrive in.
        /// </summary>
        private long NextRequestTicket() => Interlocked.Increment(ref requestTicketCounter);

        /// <summary>
        /// One grouped overlap resubmit that is running. Starting a group that contains this job's
        /// locations cancels it so a smaller, older answer cannot arrive after the larger one.
        /// Owns <see cref="Cancellation"/> until <see cref="MergeOverlapGroupAsync"/> disposes it.
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
        /// use it. Survives camera moves after upload.
        /// </summary>
        private sealed class ProcessBatch(SegmentationViewportSession session)
        {
            private int finishStarted;

            private readonly List<Task> responseTasks = [];

            public SegmentationViewportSession Session { get; } = session;

            /// <summary>Called from each request's continuation; several finish at once while requests overlap.</summary>
            public void AddResponseTask(Task task)
            {
                lock (responseTasks)
                    responseTasks.Add(task);
            }

            public Task[] SnapshotResponseTasks()
            {
                lock (responseTasks)
                    return [.. responseTasks];
            }

            /// <summary>True for the first caller. Camera-move cancel and batch finally both finish the batch.</summary>
            public bool TryBeginFinish() => Interlocked.Exchange(ref finishStarted, 1) == 0;
        }

        public AutoCirclePolygonizeController(SectionViewerControl parent, Action requestAnnotationLoad)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.requestAnnotationLoad = requestAnnotationLoad ?? throw new ArgumentNullException(nameof(requestAnnotationLoad));
        }

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
        /// Starts or stops the idle timer and location-store subscription.
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
            cache.GeometryInvalidated -= OnCacheGeometryInvalidated;
            cache.LocationForgotten -= OnCacheLocationForgotten;
            CancelUploadPhase();
            CancelProcessPhase();
            StopSettleTimers();
            idleTimer?.Dispose();
            idleTimer = null;
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in proposals.Values)
                    proposal.DisposeMaskOverlay();

                proposals.Clear();
            }
            hoveredProposal = null;
            parkedForAddPoints = null;
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
        /// Throws away the proposals, cached results and dismissals for the current section and starts a new idle scan, so
        /// every circle in view is sent to the segmentation server again. Bound to a hotkey. Does nothing while
        /// auto-polygonize is off. Call on the UI thread; the new requests run on the usual batch path.
        /// </summary>
        public void ResubmitVisible()
        {
            if (!enabled || parent.Section is null)
                return;

            int sectionNumber = parent.Section.Number;
            SegmentationDiag.Log($"AutoPolygonize.ResubmitVisible section={sectionNumber}");
            CancelProcessPhase();
            DropProposalsForSection(sectionNumber);
            cache.ClearSection(sectionNumber);
            parent.Invalidate();
            RestartIdleTimer();
        }

        /// <summary>
        /// Restarts settle when the view moved. A result on the single-resolution tile grid depends on the prompts and the fixed
        /// tile level, not on the viewport rectangle (<see cref="SegmentationCameraPolicy"/>), so the in-flight SegmentTiles
        /// call, finished proposals and the completion cache are kept: a pan or zoom only stops the queue after the current
        /// circle; the next idle batch orders the rest from the new view center.
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
            Interlocked.Increment(ref cameraGeneration);
            viewportImageLease.ForgetIfViewMoved(current, GetCurrentDownsample());
            CancelUploadPhase();
            RestartIdleTimer();
        }

        /// <summary>
        /// Drops proposals and the shared viewport capture from the previous segmentation server.
        /// When <paramref name="newEndpointIsUsable"/> and auto-polygonize is on, restarts
        /// the idle settle so the current view is uploaded to the new server.
        /// </summary>
        public void OnSegmentationEndpointChanged(bool newEndpointIsUsable)
        {
            SegmentationViewportSession.ForgetPrefetchedTiles();
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
        /// Retraces on-screen proposals from the raw server masks at the current edge-cleanup
        /// radius and hole-drop fraction. Called when those preferences are applied. Proposals
        /// that never stored a mask are left as they are.
        /// </summary>
        public void RefreshFromStoredMasks()
        {
            if (parent.InvokeRequired)
            {
                parent.BeginInvoke(new Action(RefreshFromStoredMasks));
                return;
            }

            AutoPolygonizeProposal[] snapshot;
            lock (proposalLock)
                snapshot = [.. DistinctProposalsUnlocked().Where(proposal => proposal.MaskSources.Count > 0)];

            if (snapshot.Length == 0)
                return;

            int generation = Interlocked.Increment(ref maskRefreshGeneration);
            double holeDrop = Global.AnnotationSettings.SegmentationHoleDropFraction;
            int radius = Global.AnnotationSettings.SegmentationEdgeCleanupRadius;
            double simplifyTolerance = AutoPolygonizeSelection.CreatedShapeSimplifyWorld(parent.Downsample);

            _ = Task.Run(() =>
            {
                List<(AutoPolygonizeProposal Proposal, Polygon Polygon, AutoPolygonizeMaskOverlay? Display)> rebuilt = [];
                foreach (AutoPolygonizeProposal proposal in snapshot)
                {
                    if (generation != Volatile.Read(ref maskRefreshGeneration))
                        return;

                    Geometry.Vector2 keepPoint = proposal.ForegroundPrompts.Count > 0
                        ? proposal.ForegroundPrompts[0]
                        : proposal.Polygon.Centroid;
                    if (!AutoPolygonizeMaskRetrace.TryCreate(
                            proposal.MaskSources,
                            proposal.FixedPolygons,
                            keepPoint,
                            holeDrop,
                            radius,
                            simplifyTolerance,
                            out Polygon? polygon,
                            out AutoPolygonizeMaskOverlay? display,
                            proposal.ForegroundPrompts) ||
                        polygon is null)
                    {
                        continue;
                    }

                    rebuilt.Add((proposal, polygon, display));
                }

                var dispatcher = Viking.UI.State.MainThreadDispatcher;
                if (dispatcher is null || generation != Volatile.Read(ref maskRefreshGeneration))
                    return;

                _ = dispatcher.InvokeAsync(() =>
                {
                    if (generation != Volatile.Read(ref maskRefreshGeneration))
                        return;

                    bool changed = false;
                    foreach ((AutoPolygonizeProposal proposal, Polygon polygon, AutoPolygonizeMaskOverlay? display) in rebuilt)
                    {
                        if (!IsProposalStillShown(proposal))
                            continue;

                        Geometry.Vector2 keepPoint = proposal.ForegroundPrompts.Count > 0
                            ? proposal.ForegroundPrompts[0]
                            : polygon.Centroid;
                        Polygon? carved = CarveAgainstExistingPolygons(
                            polygon,
                            proposal.LocationIds,
                            keepPoint,
                            proposal.ParentID,
                            proposal.ForegroundPrompts);
                        if (carved is null)
                            continue;

                        proposal.ReplacePolygon(
                            AutoPolygonizeSelection.SimplifyForCreatedShape(carved, parent.Downsample, proposal.ForegroundPrompts),
                            parent.Downsample);
                        proposal.SetDisplayMask(display, parent.Device);
                        changed = true;
                    }

                    if (changed)
                        parent.Invalidate();
                });
            });
        }

        /// <summary>True when <paramref name="proposal"/> is still the overlay for one of its locations.</summary>
        private bool IsProposalStillShown(AutoPolygonizeProposal proposal)
        {
            lock (proposalLock)
            {
                foreach (long id in proposal.LocationIds)
                {
                    if (proposals.TryGetValue(id, out AutoPolygonizeProposal current) &&
                        ReferenceEquals(current, proposal))
                    {
                        return true;
                    }
                }
            }

            return false;
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
            bool shimmering = false;
            lock (proposalLock)
            {
                foreach (AutoPolygonizeProposal proposal in DistinctProposalsUnlocked())
                {
                    if (proposal.SectionNumber != sectionNumber)
                        continue;

                    proposal.FadeIn = hiddenProposals.FadeIn(HiddenKey(proposal));
                    if (proposal.FadeIn <= 0f)
                        continue;

                    proposal.Draw(graphicsDevice, scene);
                    shimmering |= proposal.IsShimmering;
                }
            }

            // The shimmer animates by time, not by input, so ask for the next frame until it finishes.
            if (shimmering)
                parent.Invalidate();
        }

        /// <summary>
        /// Nearest proposal ring within the downsample-scaled hit radius.
        /// Hover and double-click call this directly. Single-click <c>ObjectAtPosition</c>
        /// prefers annotations so the circle under a ring stays editable.
        /// </summary>
        /// <returns>False when overlays are hidden or nothing is in range.</returns>
        public bool TryHit(Vector2 worldPosition, out AutoPolygonizeProposal proposal, out double distance) =>
            TryFindProposal(worldPosition, p => !hiddenProposals.IsHidden(HiddenKey(p)), out proposal, out distance);

        /// <summary>The key a proposal is hidden under: its lowest source location, which survives the proposal being rebuilt.</summary>
        private static long HiddenKey(AutoPolygonizeProposal proposal) =>
            proposal.LocationIds.Count > 0 ? proposal.LocationIds.Min() : proposal.LocationId;

        /// <summary>
        /// Del: hides the ring nearest <paramref name="worldPosition"/> (within the hit radius) for <paramref name="duration"/>.
        /// </summary>
        /// <returns>False when no visible ring is under the mouse.</returns>
        public bool TryHideProposalAt(Vector2 worldPosition, TimeSpan duration)
        {
            if (!TryHit(worldPosition, out AutoPolygonizeProposal proposal, out _))
                return false;

            hiddenProposals.Hide(HiddenKey(proposal), duration);
            if (ReferenceEquals(hoveredProposal, proposal))
            {
                proposal.IsHighlighted = false;
                hoveredProposal = null;
            }

            parent.Invalidate();
            return true;
        }

        /// <summary>
        /// Insert: starts bringing back the hidden ring nearest <paramref name="worldPosition"/>; it fades in and is
        /// pickable again when the fade ends.
        /// </summary>
        /// <returns>False when no hidden ring is under the mouse.</returns>
        public bool TryRestoreProposalAt(Vector2 worldPosition)
        {
            if (!TryFindProposal(worldPosition, p => hiddenProposals.IsHidden(HiddenKey(p)), out AutoPolygonizeProposal proposal, out _))
                return false;

            hiddenProposals.Restore(HiddenKey(proposal));
            parent.Invalidate();
            return true;
        }

        /// <summary>How long until a hidden ring next needs a redraw (its fade starts or advances), or null when none is hidden.</summary>
        public TimeSpan? TimeUntilNextHiddenRedraw() => hiddenProposals.TimeUntilNextRedraw();

        /// <summary>The ring nearest <paramref name="worldPosition"/> within the hit radius among those <paramref name="accept"/> allows.</summary>
        private bool TryFindProposal(Vector2 worldPosition, Func<AutoPolygonizeProposal, bool> accept, out AutoPolygonizeProposal proposal, out double distance)
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
                    if (candidate.SectionNumber != sectionNumber || !accept(candidate))
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
        public void Accept(AutoPolygonizeProposal proposal) => Accept(proposal, proposal?.Polygon);

        /// <summary>
        /// Writes <paramref name="polygon"/> onto the survivor for <paramref name="proposal"/>.
        /// Used by Accept polygonalization and by Paint mask after interactive refinement.
        /// </summary>
        public void Accept(AutoPolygonizeProposal proposal, Polygon? polygon)
        {
            if (proposal is null || polygon is null)
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
                ClearParkedIf(proposal);
                RemoveProposal(proposal.LocationId);
                return;
            }

            long survivorId = LocationSiblingMerge.ChooseSurvivorId(members);
            LocationObj survivor = members.First(member => member.ID == survivorId);
            List<LocationObj> victims = [.. members.Where(member => member.ID != survivorId)];

            Polygon? toApply = CarveAgainstExistingPolygons(
                polygon,
                proposal.LocationIds,
                survivor.VolumePosition,
                survivor.ParentID,
                proposal.ForegroundPrompts);
            // #region agent log
            SegmentationDiag.Log(
                $"DIAG H14 Accept ids=[{string.Join(",", proposal.LocationIds)}] members={members.Count} survivor={survivorId} " +
                $"carvedNull={toApply is null} proposalVerts={polygon.TotalUniqueVertices}");
            // #endregion
            if (toApply is null)
            {
                ClearParkedIf(proposal);
                RemoveProposal(proposal.LocationId);
                parent.Invalidate();
                return;
            }

            // Carve/union densifies; LocationShapeUpdate fits once then maps control points to mosaic.
            bool dbgApplied = LocationShapeUpdate.ApplyVolumePolygon(survivor, toApply, parent, proposal.ForegroundPrompts);
            // #region agent log
            SegmentationDiag.Log($"DIAG H14 Accept ApplyVolumePolygon survivor={survivorId} applied={dbgApplied}");
            // #endregion
            if (!dbgApplied)
            {
                // Overlay Accept keeps the ring; Paint mask restores the parked proposal so the user can retry.
                parent.ShowTransientStatus("Could not save the auto-segment outline for this annotation.");
                if (ReferenceEquals(parkedForAddPoints, proposal))
                    RestoreParkedProposal();
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
            ClearParkedIf(proposal);
            RemoveProposal(proposal.LocationId);
            CarveRemainingProposals(toApply, proposal.LocationIds);
            parent.Invalidate();
        }

        /// <summary>
        /// Parks the proposal overlay and opens <see cref="SegmentationPaintCommand"/> with the proposal's polygon already painted as
        /// foreground, so the user can paint, erase or add background before accepting. Accepting writes through
        /// <see cref="Accept(AutoPolygonizeProposal, Polygon)"/>; Escape restores the parked overlay.
        /// </summary>
        public void BeginPaintMask(AutoPolygonizeProposal proposal)
        {
            if (proposal is null || !AreProposalMembersValid(proposal))
                return;

            if (proposal.Polygon is null || proposal.Polygon.ExteriorRing.Length < 3)
            {
                MessageBox.Show(
                    "This proposal has no outline to paint from.",
                    "Paint mask",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (parkedForAddPoints is not null && !ReferenceEquals(parkedForAddPoints, proposal))
                RestoreParkedProposal();

            List<Vector2[]> rings = [proposal.Polygon.ExteriorRing, .. proposal.Polygon.InteriorRings];

            CancelProcessPhase();
            RemoveProposal(proposal.LocationId);
            parkedForAddPoints = proposal;
            parent.Invalidate();

            AutoPolygonizeProposal parked = proposal;
            void onSuccess(Polygon refined) => Accept(parked, refined);

            try
            {
                IGrpcChannelManager channelManager = ServiceLocator.GetRequiredService<IGrpcChannelManager>();
                parent.CurrentCommand = new SegmentationPaintCommand(
                    parent, onSuccess, channelManager, seedRings: rings, onAbandoned: RestoreParkedProposal);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BeginPaintMask failed: {ex.Message}");
                RestoreParkedProposal();
                MessageBox.Show(
                    $"Failed to open the mask painter: {ex.Message}",
                    "Paint mask",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>Re-publishes the proposal parked by <see cref="BeginPaintMask"/> when still valid.</summary>
        private void RestoreParkedProposal()
        {
            AutoPolygonizeProposal? parked = parkedForAddPoints;
            parkedForAddPoints = null;
            if (parked is null)
                return;

            if (!AreProposalMembersValid(parked))
            {
                parent.Invalidate();
                return;
            }

            PublishProposalOnUiThread(parked);
            parent.Invalidate();
        }

        private void ClearParkedIf(AutoPolygonizeProposal proposal)
        {
            if (ReferenceEquals(parkedForAddPoints, proposal))
                parkedForAddPoints = null;
        }

        /// <summary>Hides the proposal until each involved location's LastModified changes.</summary>
        public void Dismiss(AutoPolygonizeProposal proposal)
        {
            if (proposal is null)
                return;

            ClearParkedIf(proposal);
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

        /// <summary>
        /// Starts a fresh 500 ms idle and snapshots the view it must still show when the timer fires. Used after any
        /// real camera or section change. Runs on the UI thread, so a call from a worker is marshalled there.
        /// </summary>
        private void RestartIdleTimer()
        {
            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(RestartIdleTimer));
                return;
            }

            if (idleTimer is null)
                return;

            idleTimer.Stop();
            armedBounds = GetCurrentViewportBounds();
            armedDownsample = GetCurrentDownsample();
            armedAtUtc = DateTime.UtcNow;
            idleTimer.Start();
        }

        /// <summary>Stops the settle timer without disposing; Stop() disposes afterward.</summary>
        private void StopSettleTimers()
        {
            idleTimer?.Stop();
        }

        /// <summary>
        /// System.Timers callback. Marshals to the UI thread to compare the view with the snapshot;
        /// does not start capture here because the timer can fire during a hitch.
        /// </summary>
        private void OnIdleElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!enabled)
                return;

            Viking.UI.State.MainThreadDispatcher?.BeginInvoke(new Action(RunBatchIfViewStillMatches));
        }

        /// <summary>
        /// Starts a batch only when no camera event was processed since the idle started and the live view still
        /// matches its snapshot; otherwise restarts the idle. When the camera is coarser than
        /// <see cref="Global.AnnotationSettings.AutoPolygonizeMaxDownsample"/>, stops settling until a camera move or
        /// <see cref="RequestIdlePass"/> (preference change) restarts it.
        /// </summary>
        private void RunBatchIfViewStillMatches()
        {
            if (!enabled)
                return;

            global::WebAnnotation.UI.AnnotationStatusChips.Refresh();
            if (!IsWithinAutoSegmentDownsample())
            {
                SegmentationDiag.Log(
                    $"Idle skip: downsample out of range ds={GetCurrentDownsample()} " +
                    $"max={Global.AnnotationSettings.AutoPolygonizeMaxDownsample}");
                return;
            }

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

        /// <summary>Bumped on camera and section changes so a timed-out wait retries once per view.</summary>
        private int cameraGeneration;

        /// <summary>
        /// One capture/upload plus sequential SegmentTiles calls, nearest the cursor first. A camera move that makes
        /// results stale cancels this batch; any other move lets it finish the queued circles still on screen. An idle
        /// that fires while it runs queues a rerun, which starts as soon as this batch ends so circles that came into
        /// view are not dropped.
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
                    Viking.UI.State.MainThreadDispatcher?.BeginInvoke(new Action(RunQueuedRerun));
            }
        }

        /// <summary>
        /// Starts the batch an idle asked for while the last one ran. Skipped while a newer idle is still counting:
        /// that idle starts it. UI thread.
        /// </summary>
        private void RunQueuedRerun()
        {
            if (idleTimer is null || idleTimer.Enabled)
                return;

            RunBatchIfViewStillMatches();
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
            CancellationTokenSource? prefetchCts = null;
            ProcessBatch? processBatch = null;

            // Merge candidates this batch took off the queue; the ones it did not finish go back when the batch is cancelled.
            List<long> takenMergeCandidates = [];
            CancellationToken batchProcessToken = default;

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

                // Capturing and segmenting competes with the viewer for the texture queues, so wait until the
                // section's tiles are in. Otherwise the capture also records half-loaded tiles.
                TileWaitResult tileWait = await WaitForSectionTilesLoadedAsync(parent.Section.Number, uploadToken).ConfigureAwait(false);
                SegmentationDiag.Log($"RunBatch tileWait={tileWait} cancelled={uploadToken.IsCancellationRequested}");
                if (tileWait == TileWaitResult.TimedOut)
                {
                    int viewAtTimeout = Volatile.Read(ref cameraGeneration);
                    if (Interlocked.Exchange(ref tileTimeoutRetryGeneration, viewAtTimeout) != viewAtTimeout && enabled)
                        RestartIdleTimer();
                    return;
                }

                if (tileWait != TileWaitResult.Complete)
                    return;

                long annotationLoadMs = stepTimer.ElapsedMilliseconds;
                if (uploadToken.IsCancellationRequested || !enabled || parent.CurrentCommand is SegmentationCommand)
                {
                    SegmentationDiag.Log("RunBatch abort after annotations (cancel/disabled/command)");
                    return;
                }

                stepTimer.Restart();
                localUploadSession = new SegmentationViewportSession(parent) { EnforceAutoDownsampleLimit = true };
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

                // Taken before collecting so a stale-making camera move or a section change from here on stops this
                // batch; other camera moves leave it running (see SegmentationCameraPolicy.OnCameraMoved).
                CancellationToken processToken;
                lock (lifecycleLock)
                {
                    processToken = processCts.Token;
                }

                batchProcessToken = processToken;
                Rectangle inset = AutoPolygonizeSelection.InsetBounds(viewBounds);
                List<LocationObj> candidates = CollectEligibleCircles(viewBounds, inset);
                SegmentationDiag.Log($"RunBatch candidates={candidates.Count} bounds={viewBounds} mergeCandidates={mergeCandidateQueue.Count}");
                if (candidates.Count == 0 && mergeCandidateQueue.IsEmpty)
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
                    $"wh={uploadContext?.Width}x{uploadContext?.Height}");
                if (uploadContext is null)
                    return;

                if (uploadToken.IsCancellationRequested || !enabled || parent.CurrentCommand is SegmentationCommand)
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

                // Avoid marks come from the other annotations in view. One spatial query serves every circle until
                // the view moves.
                Rectangle promptBounds = viewBounds;
                List<LocationObj> visibleForPrompts = [.. CollectVisibleLocationObjs(viewBounds)];
                List<LocationObj> queue = candidates;

                // Tiles around every queued circle go up in the background, nearest the cursor first, so most requests
                // find them already encoded on the server. Stopped when the queue is done: what is left belongs to
                // circles no longer sent.
                prefetchCts = CancellationTokenSource.CreateLinkedTokenSource(processToken);
                LiveView startView = await ReadLiveViewAsync().ConfigureAwait(false);
                Vector2 priority = AutoPolygonizeSelection.QueuePriorityPoint(startView.Cursor, startView.Bounds);
                List<Rectangle> regions = [];
                foreach (LocationObj circle in queue.OrderBy(circle => Vector2.DistanceSquared(circle.VolumePosition, priority)))
                {
                    if (TryPrefetchRegion(circle, transform, out Rectangle region))
                        regions.Add(region);
                }

                processBatch.AddResponseTask(localUploadSession.PrefetchTilesAsync(regions, prefetchCts.Token));

                // One circle's request. Returns once its response is handed to ProcessResponseAsync.
                async Task SendCircleAsync(LocationObj circle, List<LocationObj> visibleForPrompts)
                {
                    int generation = cache.MarkPending(circle.ID, sectionNumber, circle, uploadContext);

                    Stopwatch proposalTimer = Stopwatch.StartNew();
                    if (!CircleSegmentationPrompts.TryCreateStartingPrompt(
                            CircleSegmentationPrompts.ToVolumePoints(
                                CircleSegmentationPrompts.CreateMosaicRadiusPoints(new Circle(circle.Position, circle.Radius)),
                                transform),
                            out CircleSegmentationPrompts.StartingPrompt startingPrompt))
                    {
                        return;
                    }

                    IReadOnlyList<Vector2> foreground = startingPrompt.Points;
                    IReadOnlyList<SegmentationSeed> startingBoxes = [startingPrompt.Seed];
                    IReadOnlyList<Vector2> keepPoints = [startingPrompt.Center];

                    SegmentationBackgroundShapes backgroundShapes = CircleSegmentationPrompts.CreateOtherStructureBackgroundShapes(
                        visibleForPrompts, transform, [circle.ID], circle.ParentID);
                    SegmentationDiag.Log(CircleSegmentationPrompts.DescribePrompts(
                        "auto-circle-batch", [circle.ID], circle.ParentID, visibleForPrompts, foreground, null, backgroundShapes));
                    long promptMs = proposalTimer.ElapsedMilliseconds;

                    long requestTicket = NextRequestTicket();
                    Stopwatch segmentTimer = Stopwatch.StartNew();
                    var response = await localUploadSession.SegmentAsync(
                        foreground,
                        [],
                        processToken,
                        startingBoxes,
                        requestId: (ulong)requestTicket,
                        backgroundShapes: backgroundShapes).ConfigureAwait(false);
                    long segmentMs = segmentTimer.ElapsedMilliseconds;
                    if (response is null)
                        return;

                    try
                    {
                        Task responseTask = ProcessResponseAsync(
                            processBatch.Session,
                            circle,
                            sectionNumber,
                            downsample,
                            simplifyTolerance,
                            foreground,
                            response,
                            promptMs,
                            segmentMs,
                            proposalTimer,
                            batchTimer,
                            batchId,
                            generation,
                            processToken,
                            requestTicket,
                            keepPoints,
                            startingBoxes);
                        processBatch.AddResponseTask(responseTask);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                List<Task> inFlight = [];
                while (true)
                {
                    if (processToken.IsCancellationRequested || !enabled)
                        break;

                    if (parent.CurrentCommand is SegmentationCommand)
                    {
                        CancelProcessPhase();
                        break;
                    }

                    if (inFlight.Count >= MaxRequestsInFlight)
                    {
                        Task done = await Task.WhenAny(inFlight).ConfigureAwait(false);
                        inFlight.Remove(done);
                        await done.ConfigureAwait(false);
                        continue;
                    }

                    QueuedCircle? next = await TakeNextQueuedCircleAsync(queue, sectionNumber, downsample).ConfigureAwait(false);
                    if (next is null)
                    {
                        // A response still being processed can queue merge candidates, so the batch is not over until
                        // every request and response has finished and the queue is still empty.
                        if (inFlight.Count > 0)
                        {
                            Task done = await Task.WhenAny(inFlight).ConfigureAwait(false);
                            inFlight.Remove(done);
                            await done.ConfigureAwait(false);
                            continue;
                        }

                        Task[] processing = [.. processBatch.SnapshotResponseTasks().Where(task => !task.IsCompleted)];
                        if (processing.Length == 0)
                            break;

                        await Task.WhenAny(processing).ConfigureAwait(false);
                        continue;
                    }

                    if (next.Value.IsMergeCandidate)
                    {
                        takenMergeCandidates.Add(next.Value.Circle.ID);
                        // Its surroundings may never have been on screen: load them, then take the avoid marks from there.
                        Rectangle around = await LoadRegionAroundAsync(next.Value.Circle, next.Value.LiveBounds, processToken).ConfigureAwait(false);
                        inFlight.Add(SendCircleAsync(next.Value.Circle, [.. CollectVisibleLocationObjs(around)]));
                        continue;
                    }

                    if (!SegmentationViewportSession.AreViewportBoundsSimilar(promptBounds, next.Value.LiveBounds))
                    {
                        promptBounds = next.Value.LiveBounds;
                        visibleForPrompts = [.. CollectVisibleLocationObjs(promptBounds)];
                    }

                    inFlight.Add(SendCircleAsync(next.Value.Circle, visibleForPrompts));
                }

                await Task.WhenAll(inFlight).ConfigureAwait(false);
                prefetchCts.Cancel();
                Task[] responseTasks = processBatch.SnapshotResponseTasks();
                if (responseTasks.Length > 0)
                    await Task.WhenAll(responseTasks).ConfigureAwait(false);

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
                // A camera move or section change cancelled the batch: the merge candidates it had not answered stay wanted.
                if (batchProcessToken.IsCancellationRequested)
                {
                    foreach (long id in takenMergeCandidates)
                        EnqueueMergeCandidate(id);
                }

                prefetchCts?.Cancel();
                await FinishUploadOrProcessBatchAsync(localUploadSession, processBatch).ConfigureAwait(false);
                prefetchCts?.Dispose();
            }
        }

        /// <summary>The live view bounds, camera downsample and cursor position (null when the cursor is off the view).</summary>
        private readonly record struct LiveView(Rectangle Bounds, double Downsample, Vector2? Cursor);

        /// <summary>Reads <see cref="LiveView"/> on the UI thread.</summary>
        private async Task<LiveView> ReadLiveViewAsync()
        {
            LiveView Read()
            {
                Vector2? cursor = TryGetCursorWorldPosition(out Vector2 world) ? world : null;
                return new LiveView(GetCurrentViewportBounds(), GetCurrentDownsample(), cursor);
            }

            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            return dispatcher is null || dispatcher.CheckAccess()
                ? Read()
                : await dispatcher.InvokeAsync(Read).Task.ConfigureAwait(false);
        }

        /// <summary>
        /// The world (volume) rectangle covering the mosaic square of half-width <see cref="PrefetchRadii"/> radii around
        /// <paramref name="circle"/>'s mosaic centre: the square's corners are mapped and their bounding box taken, so the
        /// transform's scale and rotation are followed without carrying the mosaic radius into volume space.
        /// </summary>
        private static bool TryPrefetchRegion(LocationObj circle, IVolumeToSectionTransform transform, out Rectangle region)
        {
            region = default;
            double reach = circle.Radius * PrefetchRadii;
            if (transform is null || reach <= 0)
                return false;

            Vector2 center = circle.Position;
            Vector2[] corners =
            [
                new(center.X - reach, center.Y - reach),
                new(center.X + reach, center.Y - reach),
                new(center.X + reach, center.Y + reach),
                new(center.X - reach, center.Y + reach),
            ];
            bool[] mapped = transform.TrySectionToVolume(corners, out Vector2[] volumeCorners);
            List<Vector2> points = [];
            for (int i = 0; i < corners.Length; i++)
            {
                if (mapped[i])
                    points.Add(volumeCorners[i]);
            }

            if (points.Count < 2)
                return false;

            region = new Rectangle(
                points.Min(point => point.X),
                points.Max(point => point.X),
                points.Min(point => point.Y),
                points.Max(point => point.Y));
            return true;
        }

        /// <summary>
        /// Loads the annotations around an off-screen <paramref name="circle"/> and returns the view-sized rectangle centred
        /// on it that the prompts are built from. Without the load, the avoid marks of the neighbouring structures there
        /// would be missing and the result cached as done.
        /// </summary>
        /// <param name="liveBounds">The live view, which gives the size.</param>
        private async Task<Rectangle> LoadRegionAroundAsync(LocationObj circle, Rectangle liveBounds, CancellationToken token)
        {
            Vector2 center = circle.VolumePosition;
            Rectangle around = new(
                new Vector2(center.X - liveBounds.Width / 2, center.Y - liveBounds.Height / 2),
                new Vector2(center.X + liveBounds.Width / 2, center.Y + liveBounds.Height / 2));
            if (parent.Scene is null || parent.Section is null)
                return around;

            var dispatcher = Viking.UI.State.MainThreadDispatcher;
            if (dispatcher is null)
                return around;

            VikingXNA.Scene? scene = await dispatcher.InvokeAsync(() =>
            {
                if (parent.Scene is null || parent.Section is null)
                    return null;

                var camera = new VikingXNA.Camera
                {
                    Downsample = parent.Camera.Downsample,
                    LookAt = new Microsoft.Xna.Framework.Vector2((float)center.X, (float)center.Y)
                };
                var offScreen = new VikingXNA.Scene(parent.Scene.Viewport, camera);
                AnnotationOverlay.GetOrCreateAnnotationsForSection(parent.Section.Number)
                    .LoadAnnotationsInRegion(offScreen, token);
                return offScreen;
            }).Task.ConfigureAwait(false);

            if (scene is not null)
                await WaitForAnnotationsLoadedAsync(token, scene).ConfigureAwait(false);

            return around;
        }

        /// <summary>World position under the mouse cursor, or false when the cursor is not over the viewer. UI thread.</summary>
        private bool TryGetCursorWorldPosition(out Vector2 world)
        {
            world = default;
            if (parent.Scene is null)
                return false;

            System.Drawing.Point client = parent.PointToClient(Control.MousePosition);
            if (!parent.ClientRectangle.Contains(client))
                return false;

            world = parent.ScreenToWorld(client.X, client.Y);
            return true;
        }

        /// <summary>The circle the queue sends next, and the live view it was picked in.</summary>
        /// <param name="IsMergeCandidate">True for a circle queued by <see cref="QueueMergeCandidates"/>, which may be off screen.</param>
        private readonly record struct QueuedCircle(LocationObj Circle, Rectangle LiveBounds, bool IsMergeCandidate = false);

        /// <summary>
        /// Removes and returns the queued circle nearest the cursor (the view centre when the cursor is off the
        /// view), reading the live camera on the UI thread. Circles that left the view, were segmented meanwhile or
        /// are no longer circles are dropped; the next batch collects any that come back. Null when nothing queued
        /// is still on screen or the camera zoomed out past the auto-segment limit.
        /// </summary>
        private async Task<QueuedCircle?> TakeNextQueuedCircleAsync(List<LocationObj> queue, int sectionNumber, double downsample)
        {
            (Rectangle liveBounds, double liveDownsample, Vector2? cursor) = await ReadLiveViewAsync().ConfigureAwait(false);

            if (liveDownsample > Global.AnnotationSettings.AutoPolygonizeMaxDownsample)
                return null;

            // Merge candidates go before anything the view or the cursor asks for, wherever they are.
            while (mergeCandidateQueue.TryDequeue(out long mergeId))
            {
                mergeCandidateIds.TryRemove(mergeId, out _);
                LocationObj candidate = Store.Locations.GetObjectByID(mergeId, false);
                if (candidate is null ||
                    candidate.Section != sectionNumber ||
                    !cache.ShouldProcess(candidate.ID, sectionNumber, candidate.LastModified, candidate.TypeCode, downsample))
                {
                    continue;
                }

                queue.RemoveAll(circle => circle.ID == candidate.ID);
                return new QueuedCircle(candidate, liveBounds, IsMergeCandidate: true);
            }

            Rectangle liveInset = AutoPolygonizeSelection.InsetBounds(liveBounds);
            queue.RemoveAll(circle =>
                !cache.ShouldProcess(circle.ID, sectionNumber, circle.LastModified, circle.TypeCode, downsample) ||
                !IsEligibleCircleInView(circle, liveBounds, liveInset));

            int index = AutoPolygonizeSelection.IndexOfNearest(
                queue,
                circle => circle.VolumePosition,
                AutoPolygonizeSelection.QueuePriorityPoint(cursor, liveBounds));
            if (index < 0)
                return null;

            LocationObj next = queue[index];
            queue.RemoveAt(index);
            return new QueuedCircle(next, liveBounds);
        }

        /// <summary>
        /// Turns one SegmentTilesStream response into a published proposal. The mask is polygonized and simplified on a
        /// worker (pure CPU work); everything that touches the location store, colors, the cache or the proposal
        /// list then runs as one UI-thread step, so no store access races a commit or a property change.
        /// Drops the result without caching if the section changed; a pan or zoom keeps it. A circle becomes one
        /// polygon, so only island 1 of the response (the island with the largest trusted area, the circle's own
        /// object) is traced; the other islands are dropped. An empty union is a LastModified skip (no overlay)
        /// so the next idle batch does not segment the same circle again. GPU mask textures are created
        /// later on the UI thread in <see cref="PublishProposalOnUiThread"/>.
        /// </summary>
        private async Task ProcessResponseAsync(
            SegmentationViewportSession session,
            LocationObj circle,
            int sectionNumber,
            double downsample,
            double simplifyTolerance,
            IReadOnlyList<Vector2> foreground,
            Viking.gRPC.SegmentationServiceTypes.V1.SegmentationResponse response,
            long promptMs,
            long segmentMs,
            Stopwatch proposalTimer,
            Stopwatch batchTimer,
            long batchId,
            int generation,
            CancellationToken processToken,
            long requestTicket,
            IReadOnlyList<Vector2>? extraKeepPoints = null,
            IReadOnlyList<SegmentationSeed>? startingBoxes = null)
        {
            if (processToken.IsCancellationRequested || !enabled || !cache.IsGenerationCurrent(circle.ID, generation))
                return;

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
                    keepComponentsContainingWorldPoints: keepPoints,
                    primaryIslandOnly: true);
                Polygon? first = polygons.FirstOrDefault();
                if (first is null)
                    return ((Polygon?)null, (AutoPolygonizeMaskOverlay?)null, 0);

                AutoPolygonizeMaskOverlay? mask = AutoPolygonizeMaskOverlay.TryCreate(session, response);
                int verticesBefore = first.TotalUniqueVertices;
                // Fit after carve on the UI thread so boolean densification is not what we draw or save.
                return (first, mask, verticesBefore);
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
                    circle.ParentID,
                    foreground);
                if (carved is null)
                {
                    RememberEmptyMask(circle, sectionNumber, session, downsample);
                    return;
                }

                Polygon ready = AutoPolygonizeSelection.SimplifyProposal(carved, simplifyTolerance, foreground);
                IReadOnlyList<Vector2> sourceKeepPoints = extraKeepPoints is { Count: > 0 }
                    ? [.. foreground, .. extraKeepPoints]
                    : foreground;
                IReadOnlyList<AutoPolygonizeMaskSource> maskSources = processed.Mask is null
                    ? []
                    : [new AutoPolygonizeMaskSource(processed.Mask, sourceKeepPoints)];
                AutoPolygonizeProposal proposal = new(
                    this,
                    circle.ID,
                    sectionNumber,
                    circle.LastModified,
                    circle.Radius,
                    ready,
                    AutoPolygonizeProposal.CreateRingViews(
                        ready,
                        AutoPolygonizeProposal.ColorForLocation(circle.ID),
                        circle.Radius,
                        downsample),
                    processed.Mask,
                    locationIds: [circle.ID],
                    parentId: circle.ParentID,
                    foregroundPrompts: foreground,
                    startingBoxes: startingBoxes,
                    maskSources: maskSources)
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

                QueueMergeCandidates(proposal);

                Debug.WriteLine(
                    $"[SegmentationProfile] Auto batch={batchId} location={circle.ID} ready-to-draw " +
                    $"prompts={promptMs}ms segmentRpc={segmentMs}ms polygonize={polygonMs}ms " +
                    $"simplifyAndViews={renderPreparationMs}ms vertices={processed.VerticesBeforeSimplify}->{ready.TotalUniqueVertices} " +
                    $"proposal={proposalTimer.ElapsedMilliseconds}ms " +
                    $"batchElapsed={batchTimer.ElapsedMilliseconds}ms foreground={foreground.Count}");
            }).Task.ConfigureAwait(false);
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

                SegmentationDiag.Log(
                    $"publish ids=[{string.Join(",", proposal.LocationIds)}] ticket={proposal.RequestTicket} round={proposal.OverlapResubmitRound} " +
                    $"area={proposal.Polygon.Area:F0} replaces=[{string.Join("; ", replaced.Select(item => string.Join(",", item.LocationIds)))}]");
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
        /// <param name="token">Cancels the wait.</param>
        /// <param name="scene">The region to wait for; null waits for the live view. An off-screen region must have been asked for with <c>LoadAnnotationsInRegion</c>.</param>
        private async Task<AnnotationWaitResult> WaitForAnnotationsLoadedAsync(CancellationToken token, VikingXNA.Scene? scene = null)
        {
            scene ??= parent.Scene;
            if (scene is null || parent.Section is null)
                return AnnotationWaitResult.Cancelled;

            SectionAnnotationsView sectionView = AnnotationOverlay.GetOrCreateAnnotationsForSection(parent.Section.Number);
            Rectangle? mosaicBounds = scene.VisibleWorldBounds.ApproximateVisibleMosaicBounds(sectionView.mapper);
            Stopwatch wait = Stopwatch.StartNew();

            while (!token.IsCancellationRequested && wait.ElapsedMilliseconds < RegionWaitTimeoutMs)
            {
                if (Store.LocationsByRegion.AreRegionQueriesComplete(
                    mosaicBounds,
                    scene.ScreenPixelSizeInVolume,
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

        /// <summary>
        /// Polls until every tile of <paramref name="sectionNumber"/> has finished downloading and become a texture.
        /// </summary>
        /// <returns>
        /// <see cref="TileWaitResult.Complete"/> when no tile of the section is pending,
        /// <see cref="TileWaitResult.TimedOut"/> when <see cref="TileWaitTimeoutMs"/> passed first, and
        /// <see cref="TileWaitResult.Cancelled"/> when cancelled or the viewer moved to another section.
        /// </returns>
        private async Task<TileWaitResult> WaitForSectionTilesLoadedAsync(int sectionNumber, CancellationToken token)
        {
            Stopwatch wait = Stopwatch.StartNew();
            while (!token.IsCancellationRequested && wait.ElapsedMilliseconds < TileWaitTimeoutMs)
            {
                if (parent.Section is null || parent.Section.Number != sectionNumber)
                    return TileWaitResult.Cancelled;

                if (global::Viking.SectionTileLoadStatus.IsLoaded(sectionNumber))
                    return TileWaitResult.Complete;

                try
                {
                    await Task.Delay(RegionPollMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return TileWaitResult.Cancelled;
                }
            }

            return token.IsCancellationRequested ? TileWaitResult.Cancelled : TileWaitResult.TimedOut;
        }

        /// <summary>Outcome of <see cref="WaitForSectionTilesLoadedAsync"/>.</summary>
        private enum TileWaitResult
        {
            Complete,
            TimedOut,
            Cancelled
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

        /// <summary>View generation for which <see cref="RunBatchBodyAsync"/> already retried after a tile-load timeout.</summary>
        private int tileTimeoutRetryGeneration = -1;

        /// <summary>
        /// Circles whose center is at least 5% from each edge, whose disk is fully
        /// on screen, large enough, and not already proposed or dismissed for this LastModified.
        /// Unordered: <see cref="TakeNextQueuedCircleAsync"/> picks the nearest to the cursor before each request.
        /// </summary>
        private List<LocationObj> CollectEligibleCircles(Rectangle viewBounds, Rectangle inset)
        {
            List<LocationObj> eligible = [];
            foreach (LocationObj loc in CollectVisibleLocationObjs(viewBounds))
            {
                if (!cache.ShouldProcess(loc.ID, loc.Section, loc.LastModified, loc.TypeCode, GetCurrentDownsample()))
                    continue;

                if (!IsEligibleCircleInView(loc, viewBounds, inset))
                    continue;

                eligible.Add(loc);
            }

            return eligible;
        }

        /// <summary>
        /// A circle auto-polygonize may send for this view: away from the edges, wholly on screen and above the
        /// minimum radius. See <see cref="AutoPolygonizeSelection"/>.IsEligibleCircle.
        /// </summary>
        private static bool IsEligibleCircleInView(LocationObj loc, Rectangle viewBounds, Rectangle inset)
        {
            return AutoPolygonizeSelection.IsEligibleCircle(
                loc.TypeCode,
                loc.VolumePosition,
                inset,
                viewBounds,
                loc.Radius,
                Global.Scale.X,
                Global.AnnotationSettings.AutoPolygonizeMinRadiusNanometers);
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
            toCancel.ClearUploadedView();
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
        /// phase, wait for response tasks; otherwise cancel and forget the unused upload.
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
            localUploadSession.ClearUploadedView();
        }

        /// <summary>Waits for remaining polygonize tasks, then drops the batch from the live set.</summary>
        private async Task FinishProcessBatchAsync(ProcessBatch batch)
        {
            if (!batch.TryBeginFinish())
                return;

            try
            {
                Task[] pending = batch.SnapshotResponseTasks();
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
            DissolveProposal(locationId);
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
            DissolveProposal(locationId);
            if (enabled)
                parent.Invalidate();
        }

        /// <summary>
        /// Single-ID refresh after a translate/scale/resize. Reuses the shared viewport capture when the
        /// live view still matches it; otherwise captures the current view. Missing-tile re-upload
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
            try
            {
                AutoPolygonizeUploadContext? uploaded = await CaptureSharedViewportAsync(
                    session,
                    viewBounds,
                    downsample,
                    processToken).ConfigureAwait(false);
                if (uploaded is null)
                    return;

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
                IReadOnlyList<SegmentationSeed> startingBoxes = [startingPrompt.Seed];
                IReadOnlyList<Vector2> keepPoints = [startingPrompt.Center];

                List<LocationObj> visibleForPrompts = [.. CollectVisibleLocationObjs(viewBounds)];
                SegmentationBackgroundShapes backgroundShapes = CircleSegmentationPrompts.CreateOtherStructureBackgroundShapes(
                    visibleForPrompts, transform, [circle.ID], circle.ParentID);
                SegmentationDiag.Log(CircleSegmentationPrompts.DescribePrompts(
                    "auto-circle", [circle.ID], circle.ParentID, visibleForPrompts, foreground, null, backgroundShapes));

                Stopwatch proposalTimer = Stopwatch.StartNew();
                long requestTicket = NextRequestTicket();
                var response = await session.SegmentAsync(
                    foreground,
                    [],
                    processToken,
                    startingBoxes,
                    requestId: (ulong)requestTicket,
                    backgroundShapes: backgroundShapes).ConfigureAwait(false);
                if (response is null)
                    return;

                downsample = GetCurrentDownsample();
                AutoPolygonizeUploadContext? afterSegment = SharedViewportImageLease.TryCreateContext(session);
                int liveGeneration = cache.MarkPending(circle.ID, sectionNumber, circle, afterSegment);
                await ProcessResponseAsync(
                    session,
                    circle,
                    sectionNumber,
                    downsample,
                    AutoPolygonizeSelection.CreatedShapeSimplifyWorld(parent.Downsample),
                    foreground,
                    response,
                    0,
                    0,
                    proposalTimer,
                    proposalTimer,
                    0,
                    liveGeneration,
                    processToken,
                    requestTicket,
                    keepPoints,
                    startingBoxes).ConfigureAwait(false);
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
                session.ClearUploadedView();
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
        /// Reuses a viewport-similar upload or runs one capture shared with <see cref="SegmentationCommand"/>.
        /// A <paramref name="session"/> that joined another caller's capture records the view itself.
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

            if (!ready.MatchesViewportForReuse(GetCurrentViewportBounds(), GetCurrentDownsample()))
            {
                viewportImageLease.ForgetIfViewMoved(GetCurrentViewportBounds(), GetCurrentDownsample());
                return null;
            }

            if (!session.HasUploadedTiles)
            {
                session.ViewportBounds = viewBounds;
                if (!await session.UploadCurrentImageAsync(cancellationToken).ConfigureAwait(false))
                    return null;
                return SharedViewportImageLease.TryCreateContext(session);
            }

            return ready;
        }

        /// <summary>
        /// Runs only for the capture starter. Joined callers wait on the same task and then record the view.
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

        /// <summary>
        /// Removes the proposal that stands for <paramref name="changedId"/> and, when it is a merge of several circles,
        /// puts the proposals it replaced back on screen, except those that contain the changed circle. The changed circle
        /// is re-segmented by its own refresh, whose publish re-merges the group. Without this the whole group vanished and
        /// the other circles stayed blank, because their cache entries still said they were done. UI thread.
        /// </summary>
        private void DissolveProposal(long changedId)
        {
            List<AutoPolygonizeProposal> restored = [];
            AutoPolygonizeProposal? dissolved = null;
            lock (proposalLock)
            {
                if (!proposals.TryGetValue(changedId, out dissolved))
                    return;

                if (ReferenceEquals(hoveredProposal, dissolved))
                    hoveredProposal = null;

                dissolved.DisposeMaskOverlay();
                foreach (long id in dissolved.LocationIds)
                    proposals.Remove(id);
                proposals.Remove(changedId);

                List<AutoPolygonizeProposal> survivors = [];
                CollectSurvivors(dissolved, changedId, survivors);
                foreach (AutoPolygonizeProposal survivor in survivors)
                {
                    if (survivor.LocationIds.Any(proposals.ContainsKey))
                        continue;

                    survivor.EndRefining();
                    if (Global.AnnotationSettings.AutoPolygonizeOverlayMasks)
                        survivor.AttachMaskOverlay(parent.Device);

                    foreach (long id in survivor.LocationIds)
                        proposals[id] = survivor;
                    restored.Add(survivor);
                }
            }

            SegmentationDiag.Log(
                $"dissolve changed={changedId} removed=[{string.Join(",", dissolved.LocationIds)}] " +
                $"restored=[{string.Join("; ", restored.Select(item => string.Join(",", item.LocationIds)))}]");
        }

        /// <summary>
        /// The proposals a merged proposal replaced that do not contain <paramref name="changedId"/>. A member that does
        /// contain it and is itself a merge is searched in turn; one that is a single request result is dropped.
        /// </summary>
        internal static void CollectSurvivors(AutoPolygonizeProposal merged, long changedId, List<AutoPolygonizeProposal> survivors)
        {
            foreach (AutoPolygonizeProposal member in merged.MergedFrom)
            {
                if (!member.LocationIds.Contains(changedId))
                    survivors.Add(member);
                else
                    CollectSurvivors(member, changedId, survivors);
            }
        }

        private IEnumerable<AutoPolygonizeProposal> DistinctProposalsUnlocked() =>
            proposals.Values.Distinct();

        /// <summary>
        /// Queues, at the front of the line, every circle of the result's structure that lies within the result's average
        /// diameter of it, on screen or not and whether or not it was in this batch: it may be the same cell and the result a
        /// merge of the two. Each one's own result is checked the same way, so the search follows a chain of circles until
        /// none is left near an answer; a circle is only segmented once, so it ends. UI thread, after a publish.
        /// </summary>
        private void QueueMergeCandidates(AutoPolygonizeProposal proposal)
        {
            if (proposal.ParentID is not long structureId || !enabled)
                return;

            int sectionNumber = proposal.SectionNumber;
            Polygon result = proposal.Polygon;
            IReadOnlyCollection<long> own = proposal.LocationIds;
            double downsample = GetCurrentDownsample();
            ObserveFaults(Task.Run(() =>
            {
                DateTime now = DateTime.UtcNow;
                if (!structureFetchedUtc.TryGetValue(structureId, out DateTime fetched) || now - fetched > StructureFetchLifetime)
                {
                    // Brings the structure's circles on every section into the store; most are off screen.
                    Store.Locations.GetLocationsForStructure(structureId);
                    structureFetchedUtc[structureId] = now;
                }

                List<AutoPolygonizeSelection.CircleFootprint> near = AutoPolygonizeSelection.CollectMergeCandidates(
                    result,
                    structureId,
                    sectionNumber,
                    AutoPolygonizeSelection.ToCircleFootprints(Store.Locations.GetLocalObjectsForStructure(structureId)),
                    own);
                int queued = 0;
                foreach (AutoPolygonizeSelection.CircleFootprint footprint in near)
                {
                    LocationObj loc = Store.Locations.GetObjectByID(footprint.Id, false);
                    if (loc is null ||
                        !cache.ShouldProcess(loc.ID, sectionNumber, loc.LastModified, loc.TypeCode, downsample) ||
                        !AutoPolygonizeSelection.MeetsMinRadiusNanometers(
                            loc.Radius,
                            Global.Scale.X,
                            Global.AnnotationSettings.AutoPolygonizeMinRadiusNanometers))
                    {
                        continue;
                    }

                    lock (proposalLock)
                    {
                        if (proposals.ContainsKey(loc.ID))
                            continue;
                    }

                    if (EnqueueMergeCandidate(loc.ID))
                        queued++;
                }

                SegmentationDiag.Log(
                    $"merge check ids=[{string.Join(",", own)}] structure={structureId} diameter={AutoPolygonizeSelection.AverageDiameter(result):F0} " +
                    $"near={near.Count} queued={queued}");
                if (queued > 0 && !IsBusy)
                    RestartIdleTimer();
            }), "merge candidate check");
        }

        /// <summary>Queues one circle for the merge check unless it is already queued. Thread-safe.</summary>
        private bool EnqueueMergeCandidate(long locationId)
        {
            if (!mergeCandidateIds.TryAdd(locationId, 0))
                return false;

            mergeCandidateQueue.Enqueue(locationId);
            return true;
        }

        /// <summary>
        /// After a publish, if other same-cell overlays or already-saved sibling polygons
        /// nested-contain or cross this one, merge their logit fields into one proposal (no server request).
        /// Runs again on the merged result, so groups that come to overlap are grouped in turn, up to the
        /// <see cref="AutoPolygonizeSelection.MaxOverlapResubmitRound"/> backstop.
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
        /// Starts the logit merge (<see cref="MergeOverlapGroupAsync"/>) for a same-cell component of 2+ locations.
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

            // The members are drawn from single-circle masks and are about to be replaced by the group's mask.
            // They show as provisional until the resubmit ends, whether it publishes, finds nothing or is cancelled.
            foreach (AutoPolygonizeProposal member in members)
                member.BeginRefining();

            parent.Invalidate();
            ObserveFaults(MergeOverlapGroupAsync(members, locationIds, currentRound + 1, key, job), "overlap merge");
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
        /// One logit merge per overlapping saved-sibling component in the live view.
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
            long? excludeParentId = null,
            IReadOnlyList<Vector2>? mustContain = null)
        {
            if (proposed is null || parent.Section is null)
                return proposed;

            IReadOnlyCollection<LocationObj> nearby = [.. CollectVisibleLocationObjs(proposed.BoundingBox)];
            List<Polygon> existing = AutoPolygonizeSelection.CollectOverlappingExistingPolygons(
                nearby,
                proposed,
                excludeLocationIds,
                parent.Section.ActiveSectionToVolumeTransform,
                parent.Section.Number,
                excludeParentId);
            // Circle annotations of other structures count as well, so a cell that is still only a circle is not painted over.
            List<Polygon> circleDisks = AutoPolygonizeSelection.CollectOverlappingCircleDisks(
                AutoPolygonizeSelection.ToCircleFootprints(nearby),
                proposed,
                excludeLocationIds,
                parent.Section.Number,
                excludeParentId,
                keepPoint);
            existing.AddRange(circleDisks);
            Polygon? carved = AutoPolygonizeSelection.SubtractOverlappingPolygons(proposed, existing, keepPoint, mustContain);
            SegmentationDiag.Log(
                $"Carve exclude=[{string.Join(",", excludeLocationIds)}] keepPoint={keepPoint} " +
                $"proposed area={proposed.Area:F0} bbox={proposed.BoundingBox} vertices={proposed.TotalUniqueVertices}; " +
                $"overlapping existing={existing.Count} (circles={circleDisks.Count}) [{string.Join("; ", existing.Select(polygon => $"area={polygon.Area:F0} bbox={polygon.BoundingBox}"))}]; " +
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
                Polygon? carved = CarveAgainstExistingPolygons(other.Polygon, other.LocationIds, keep, other.ParentID, other.ForegroundPrompts);
                if (carved is null)
                {
                    RemoveProposal(other.LocationId);
                    continue;
                }

                if (!ReferenceEquals(carved, other.Polygon))
                {
                    other.ReplacePolygon(
                        AutoPolygonizeSelection.SimplifyForCreatedShape(carved, downsample, other.ForegroundPrompts),
                        downsample);
                }
            }
        }

        /// <summary>
        /// Merges overlapping same-structure proposals without asking the server again: the members' logit fields are
        /// combined by per-pixel maximum (<see cref="AutoPolygonizeMaskOverlay.TryOr"/>) and the result is traced once
        /// at the mask threshold. Replaces the member overlays with a single group proposal stored under every involved
        /// location ID. Saved sibling polygons that have no mask are unioned into the outline as fixed rings.
        /// </summary>
        /// <remarks>
        /// Runs on a worker thread and finishes on the UI thread. <paramref name="job"/> is cancelled when a larger
        /// group containing these locations starts, so a smaller merge cannot publish after a larger one.
        /// </remarks>
        private async Task MergeOverlapGroupAsync(
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

                CancellationToken lifecycleToken;
                lock (lifecycleLock)
                {
                    lifecycleToken = processCts.Token;
                }

                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken, job.Cancellation.Token);
                CancellationToken processToken = linkedCancellation.Token;
                if (processToken.IsCancellationRequested)
                    return;

                double downsample = GetCurrentDownsample();
                IReadOnlyList<Polygon> promptPolygons = CollectOverlapPromptPolygons(members, locationIds);
                IReadOnlyList<Vector2> foreground = CircleSegmentationPrompts.CreateGroupPromptFromPolygons(promptPolygons).Foreground;
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

                List<Polygon> originalPolygons = [.. promptPolygons];
                AutoPolygonizeMaskOverlay? mergedMask = AutoPolygonizeMaskOverlay.TryOr(
                    [.. members.Select(member => member.MaskOverlay)]);

                // A ring that was not traced from a mask (a saved sibling, or a proposal without one) is unioned as is.
                List<Polygon> fixedPolygons = [];
                foreach (Polygon shape in originalPolygons)
                {
                    bool tracedFromMask = members.Any(member =>
                        member.MaskSources.Count > 0 && ReferenceEquals(member.Polygon, shape));
                    if (!tracedFromMask)
                        fixedPolygons.Add(shape);
                }

                List<AutoPolygonizeMaskSource> maskSources = mergedMask is null
                    ? []
                    : [new AutoPolygonizeMaskSource(mergedMask, foreground)];
                double simplifyTolerance = AutoPolygonizeSelection.CreatedShapeSimplifyWorld(parent.Downsample);
                double holeDrop = Global.AnnotationSettings.SegmentationHoleDropFraction;
                int cleanupRadius = Global.AnnotationSettings.SegmentationEdgeCleanupRadius;
                Vector2 unionKeepPoint = originalPolygons.Count > 0 ? originalPolygons[0].Centroid : foreground[0];

                Polygon? merged = await Task.Run(() =>
                {
                    if (maskSources.Count == 0)
                    {
                        Polygon? union = AutoPolygonizeSelection.UnionPolygons([.. originalPolygons], unionKeepPoint);
                        return union is null ? null : AutoPolygonizeSelection.SimplifyProposal(union, simplifyTolerance, foreground);
                    }

                    return AutoPolygonizeMaskRetrace.TryCreate(
                        maskSources,
                        fixedPolygons,
                        unionKeepPoint,
                        holeDrop,
                        cleanupRadius,
                        simplifyTolerance,
                        out Polygon? traced,
                        out _,
                        foreground)
                        ? traced
                        : null;
                }).ConfigureAwait(false);
                if (processToken.IsCancellationRequested)
                    return;

                SegmentationDiag.Log(
                    $"auto-overlap-merge ids=[{string.Join(",", locationIds)}] masks={maskSources.Count} fixed={fixedPolygons.Count} " +
                    $"merged={(merged is null ? "null" : $"area={merged.Area:F0}")} " +
                    $"mask={(mergedMask is null ? "none" : $"{mergedMask.Width}x{mergedMask.Height}")}");

                var dispatcher = Viking.UI.State.MainThreadDispatcher;
                if (dispatcher is null)
                    return;

                await dispatcher.InvokeAsync(() =>
                {
                    if (processToken.IsCancellationRequested || !locationIds.All(IsGroupMemberValid))
                        return;

                    Polygon? polygon = merged;
                    if (polygon is not null)
                    {
                        LocationObj? keepLocation = Store.Locations.GetObjectByID(locationIds[0], false);
                        Vector2 keepPoint = keepLocation?.VolumePosition ?? unionKeepPoint;
                        polygon = CarveAgainstExistingPolygons(polygon, locationIds, keepPoint, parentId, foreground);
                        if (polygon is not null)
                            polygon = AutoPolygonizeSelection.SimplifyProposal(polygon, simplifyTolerance, foreground);
                    }

                    // Members keep their upload so the group does not lose the image its masks were captured from.
                    AutoPolygonizeUploadContext? groupUpload = null;
                    foreach (long id in locationIds)
                    {
                        if (cache.TryGetUploadContext(id, out AutoPolygonizeUploadContext existing))
                        {
                            groupUpload = existing;
                            break;
                        }
                    }

                    if (polygon is null)
                    {
                        RememberGroup(locationIds, sectionNumber, lastModified, groupUpload, downsample);
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
                        startingBoxes: [],
                        maskSources,
                        fixedPolygons,
                        members)
                    {
                        RequestTicket = job.Ticket
                    };

                    PublishProposalOnUiThread(group, () => RememberGroup(locationIds, sectionNumber, lastModified, groupUpload, downsample));
                }).Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto polygonize overlap merge failed: {ex.Message}");
            }
            finally
            {
                lock (overlapResubmitsInFlight)
                    overlapResubmitsInFlight.Remove(inFlightKey);
                linkedCancellation?.Dispose();
                job.Cancellation.Dispose();
                foreach (AutoPolygonizeProposal member in members)
                    member.EndRefining();

                parent.Invalidate();
            }
        }

        /// <summary>
        /// Records every ID of a merged group in the cache so none of them is segmented again until its
        /// <c>LastModified</c> changes. Each ID keeps the upload it already has; the others take
        /// <paramref name="fallbackUpload"/>. UI thread.
        /// </summary>
        private void RememberGroup(
            IReadOnlyList<long> locationIds,
            int sectionNumber,
            DateTime lastModifiedFallback,
            AutoPolygonizeUploadContext? fallbackUpload,
            double downsample)
        {
            foreach (long id in locationIds)
            {
                LocationObj loc = Store.Locations.GetObjectByID(id, false);
                AutoPolygonizeUploadContext? upload = cache.TryGetUploadContext(id, out AutoPolygonizeUploadContext own)
                    ? own
                    : fallbackUpload;
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
