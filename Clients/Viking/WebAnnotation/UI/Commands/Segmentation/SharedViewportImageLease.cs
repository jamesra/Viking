using Geometry;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// One SAM2 viewport capture shared by auto-polygonize and <see cref="SegmentationCommand"/>.
    /// Keyed by world bounds and downsample: a caller whose view matches the published capture reuses it,
    /// and a caller whose view matches a capture still in flight joins it instead of starting another.
    /// </summary>
    internal sealed class SharedViewportImageLease
    {
        private readonly object gate = new();
        private AutoPolygonizeUploadContext? published;
        private Task<AutoPolygonizeUploadContext?>? inFlight;
        private TaskCompletionSource<AutoPolygonizeUploadContext?>? inFlightSource;
        private Rectangle inFlightBounds;
        private double inFlightDownsample;
        private int uploadGeneration;

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
        /// Copies a finished upload into the lease shape. Null when the session never recorded tile dimensions.
        /// Downsample is the session mosaic level (resolved from camera), not a raw fractional zoom.
        /// </summary>
        public static AutoPolygonizeUploadContext? TryCreateContext(SegmentationViewportSession session)
        {
            if (session is null || session.UploadedImageWidth <= 0 || session.UploadedImageHeight <= 0)
                return null;

            Rectangle bounds = session.UploadedImageBounds ?? session.ViewportBounds;
            return new AutoPolygonizeUploadContext(
                session.MosaicDownsample,
                bounds,
                session.UploadedImageWidth,
                session.UploadedImageHeight);
        }

        /// <summary>
        /// Drops the published capture when the live view no longer matches it.
        /// Called on camera move and when a capture finishes for a view the camera has already left.
        /// </summary>
        public void ForgetIfViewMoved(Rectangle liveBounds, double liveDownsample)
        {
            lock (gate)
            {
                if (published is { } current && !current.MatchesViewportForReuse(liveBounds, liveDownsample))
                    published = null;
            }
        }

        /// <summary>
        /// Drops the published capture. Used when the segmentation server changes so the next capture
        /// starts over against the new server.
        /// </summary>
        public void AbandonPublished()
        {
            lock (gate)
                published = null;
        }

        /// <summary>
        /// Unhooks the in-flight slot so a new capture can start. Waiters of the abandoned upload
        /// still receive its result and must reject it when the live view no longer matches.
        /// A late success does not replace a newer published capture.
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
        /// Returns a matching published capture, joins an in-flight capture of the same view, or runs
        /// <paramref name="upload"/> once and publishes its result until the view moves.
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
                if (published is { } current && current.MatchesViewportForReuse(liveBounds, liveDownsample))
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
            => SegmentationViewportSession.LiveCameraViewMatches(
                inFlightBounds,
                inFlightDownsample,
                liveBounds,
                liveDownsample);

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

            FinishInFlightIfCurrent(generation, mine, result is { IsUsable: true } usable ? usable : null);

            mine.TrySetResult(result);
            return result;
        }

        /// <summary>
        /// Clears the in-flight slot when <paramref name="mine"/> still owns it and publishes
        /// <paramref name="usable"/> if there is one. An abandoned capture changes nothing.
        /// </summary>
        private void FinishInFlightIfCurrent(
            int generation,
            TaskCompletionSource<AutoPolygonizeUploadContext?> mine,
            AutoPolygonizeUploadContext? usable)
        {
            lock (gate)
            {
                if (generation != uploadGeneration)
                    return;

                if (ReferenceEquals(inFlight, mine.Task))
                    inFlight = null;

                if (ReferenceEquals(inFlightSource, mine))
                    inFlightSource = null;

                if (usable.HasValue)
                    published = usable;
            }
        }
    }
}
