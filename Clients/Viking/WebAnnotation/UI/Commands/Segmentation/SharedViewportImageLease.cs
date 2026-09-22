using Geometry;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// One SAM2 viewport image shared by auto-polygonize and <see cref="SegmentationCommand"/>.
    /// Keyed by world bounds and downsample. Callers take their own
    /// <see cref="AutoPolygonizeCache.AcquireBatchHold"/> while they use the id; this lease
    /// keeps one hold so the image survives until the view moves. <see cref="AutoPolygonizeCache.ImageLeaseReleased"/>
    /// must call <see cref="Forget"/> so a deleted id is not adopted again.
    /// </summary>
    internal sealed class SharedViewportImageLease
    {
        private readonly object gate = new();
        private readonly AutoPolygonizeCache cache;
        private AutoPolygonizeUploadContext? published;
        private Task<AutoPolygonizeUploadContext?>? inFlight;
        private TaskCompletionSource<AutoPolygonizeUploadContext?>? inFlightSource;
        private Rectangle inFlightBounds;
        private double inFlightDownsample;
        private int uploadGeneration;
        private ulong? leaseHoldId;

        /// <summary>
        /// Binds the lease to the cache that owns image holds. Called from the auto-polygonize controller.
        /// </summary>
        public SharedViewportImageLease(AutoPolygonizeCache cache)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>True while a capture of some viewport has been started and not abandoned or finished.</summary>
        public bool HasInFlight
        {
            get
            {
                lock (gate)
                    return inFlight is not null;
            }
        }

        /// <summary>
        /// True when <paramref name="context"/> is the same screen: bounds within 1% of the longer side
        /// and downsample has not moved by 2×. Used by auto-polygonize and segmentation commands.
        /// </summary>
        public static bool CanReuse(in AutoPolygonizeUploadContext context, Rectangle liveBounds, double liveDownsample)
        {
            if (!context.IsUsable)
                return false;

            if (AutoPolygonizeSelection.DownsampleChangedByFactorOfTwo(liveDownsample, context.Downsample))
                return false;

            return SegmentationViewportSession.AreViewportBoundsSimilar(context.WorldBounds, liveBounds);
        }

        /// <summary>
        /// Copies a finished upload into the lease shape. Null when the session never recorded a server image.
        /// </summary>
        public static AutoPolygonizeUploadContext? TryCreateContext(SegmentationViewportSession session, double downsample)
        {
            if (session?.CurrentImageId is not ulong imageId || imageId == 0 || session.UploadedImageWidth <= 0)
                return null;

            Rectangle bounds = session.UploadedImageBounds ?? session.ViewportBounds;
            return new AutoPolygonizeUploadContext(
                imageId,
                downsample,
                bounds,
                session.UploadedImageWidth,
                session.UploadedImageHeight);
        }

        /// <summary>
        /// Returns the published image when it still matches the live view. Does not start an upload.
        /// </summary>
        public bool TryAdopt(Rectangle liveBounds, double liveDownsample, out AutoPolygonizeUploadContext context)
        {
            lock (gate)
            {
                if (published is { } current && CanReuse(current, liveBounds, liveDownsample))
                {
                    context = current;
                    return true;
                }
            }

            context = default;
            return false;
        }

        /// <summary>
        /// Records an upload another path already finished (per-circle refresh) so the next command can adopt it.
        /// </summary>
        public void Publish(in AutoPolygonizeUploadContext context)
        {
            if (!context.IsUsable)
                return;

            RememberPublished(context);
        }

        /// <summary>
        /// Drops the published id when the cache released its last hold. Does not release again.
        /// </summary>
        public void Forget(ulong imageId)
        {
            lock (gate)
            {
                if (published is { } current && current.ImageId == imageId)
                    published = null;

                if (leaseHoldId == imageId)
                    leaseHoldId = null;
            }
        }

        /// <summary>
        /// Releases this lease's hold when the live view no longer matches the published image.
        /// Called on camera move and when a segmentation command sees the view leave the 1% test.
        /// </summary>
        public void ForgetIfViewMoved(Rectangle liveBounds, double liveDownsample)
        {
            ulong? releaseId = null;
            lock (gate)
            {
                if (published is not { } current || CanReuse(current, liveBounds, liveDownsample))
                    return;

                published = null;
                if (leaseHoldId is ulong id)
                {
                    releaseId = id;
                    leaseHoldId = null;
                }
            }

            if (releaseId is ulong imageId)
                cache.ReleaseBatchHold(imageId);
        }

        /// <summary>
        /// Drops the published image and this lease's hold. Used when the segmentation server
        /// changes so the next capture cannot adopt an id that only exists on the previous server.
        /// </summary>
        public void AbandonPublished()
        {
            ulong? releaseId = null;
            lock (gate)
            {
                published = null;
                if (leaseHoldId is ulong id)
                {
                    releaseId = id;
                    leaseHoldId = null;
                }
            }

            if (releaseId is ulong imageId)
                cache.ReleaseBatchHold(imageId);
        }

        /// <summary>
        /// Unhooks the in-flight slot so a new capture can start. Waiters of the abandoned upload
        /// still receive its result and must reject it when the live view no longer matches.
        /// A late success does not replace a newer published image.
        /// </summary>
        public void AbandonInFlight()
        {
            lock (gate)
            {
                uploadGeneration++;
                inFlight = null;
                inFlightSource = null;
            }
        }

        /// <summary>
        /// Adopts a matching image, joins an in-flight capture of the same view, or runs
        /// <paramref name="upload"/> once. The lease holds the id until the view moves or the cache forgets it.
        /// </summary>
        public Task<AutoPolygonizeUploadContext?> GetOrUploadAsync(
            Rectangle liveBounds,
            double liveDownsample,
            Func<Task<AutoPolygonizeUploadContext?>> upload)
        {
            if (upload is null)
                throw new ArgumentNullException(nameof(upload));

            TaskCompletionSource<AutoPolygonizeUploadContext?> mine;
            int generation;
            lock (gate)
            {
                if (published is { } current && CanReuse(current, liveBounds, liveDownsample))
                    return Task.FromResult<AutoPolygonizeUploadContext?>(current);

                if (inFlight is not null && InFlightMatches(liveBounds, liveDownsample))
                    return inFlight;

                generation = ++uploadGeneration;
                mine = new TaskCompletionSource<AutoPolygonizeUploadContext?>(TaskCreationOptions.RunContinuationsAsynchronously);
                inFlightSource = mine;
                inFlight = mine.Task;
                inFlightBounds = liveBounds;
                inFlightDownsample = liveDownsample;
            }

            return FinishUploadAsync(mine, generation, upload);
        }

        private bool InFlightMatches(Rectangle liveBounds, double liveDownsample)
        {
            if (AutoPolygonizeSelection.DownsampleChangedByFactorOfTwo(liveDownsample, inFlightDownsample))
                return false;

            return SegmentationViewportSession.AreViewportBoundsSimilar(inFlightBounds, liveBounds);
        }

        private async Task<AutoPolygonizeUploadContext?> FinishUploadAsync(
            TaskCompletionSource<AutoPolygonizeUploadContext?> mine,
            int generation,
            Func<Task<AutoPolygonizeUploadContext?>> upload)
        {
            AutoPolygonizeUploadContext? result = null;
            try
            {
                result = await upload().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                result = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Shared viewport upload failed: {ex.Message}");
                result = null;
            }

            if (result is { IsUsable: true } usable)
                RememberPublishedIfCurrent(generation, mine, usable);
            else
                ClearInFlightIfCurrent(generation, mine);

            mine.TrySetResult(result);
            return result;
        }

        private void ClearInFlightIfCurrent(int generation, TaskCompletionSource<AutoPolygonizeUploadContext?> mine)
        {
            lock (gate)
            {
                if (generation != uploadGeneration)
                    return;

                if (ReferenceEquals(inFlight, mine.Task))
                    inFlight = null;

                if (ReferenceEquals(inFlightSource, mine))
                    inFlightSource = null;
            }
        }

        private void RememberPublishedIfCurrent(
            int generation,
            TaskCompletionSource<AutoPolygonizeUploadContext?> mine,
            AutoPolygonizeUploadContext context)
        {
            bool stillCurrent;
            lock (gate)
            {
                stillCurrent = generation == uploadGeneration;
                if (stillCurrent)
                {
                    if (ReferenceEquals(inFlight, mine.Task))
                        inFlight = null;

                    if (ReferenceEquals(inFlightSource, mine))
                        inFlightSource = null;
                }
            }

            if (stillCurrent)
                RememberPublished(context);
        }

        private void RememberPublished(in AutoPolygonizeUploadContext context)
        {
            ulong? acquireId = null;
            ulong? releaseId = null;
            lock (gate)
            {
                published = context;
                if (leaseHoldId != context.ImageId)
                {
                    releaseId = leaseHoldId;
                    leaseHoldId = context.ImageId;
                    acquireId = context.ImageId;
                }
            }

            if (acquireId is ulong acquire)
                cache.AcquireBatchHold(acquire);

            if (releaseId is ulong release)
                cache.ReleaseBatchHold(release);
        }
    }
}
