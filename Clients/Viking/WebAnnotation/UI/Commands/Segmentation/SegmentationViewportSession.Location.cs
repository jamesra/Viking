using Geometry;
using Grpc.Core;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.VolumeModel;
using CoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using Polygon = Geometry.Polygon;

namespace WebAnnotation.UI.Commands.Segmentation
{
    internal sealed partial class SegmentationViewportSession
    {
        /// <summary>
        /// A <c>SegmentLocation</c> answer for the viewer: the segmentation on the viewer's grid, and the outline a person
        /// reviewed for the location, when the server holds one, in viewer space.
        /// </summary>
        /// <param name="Response">The islands' logits on the viewer's grid, like a <see cref="SegmentAsync"/> response.</param>
        /// <param name="Reviewed">The reviewed outline in viewer space; null when there is none or it does not map.</param>
        internal sealed record LocationSegmentation(SegmentationResponse Response, Polygon? Reviewed);

        /// <summary>
        /// Segments a saved location the server builds the prompt for (<c>SegmentLocation</c>): the server reads the location,
        /// its neighbours and the images itself, so this client sends only the location and the threshold and says it provides
        /// no images. Only for a context <see cref="ServerReadsContext"/> lists. The result, on the mosaic grid at downsample 1,
        /// is redrawn on the viewer's grid (<see cref="SegmentationFieldResampler"/>) and then handled like a
        /// <see cref="SegmentAsync"/> response: the same tile level is recorded, and the cells under it are captured for the edge
        /// snap. Returns null when no mask could be produced; <see cref="ConsumeLastSkip"/> says why.
        /// </summary>
        /// <param name="requestId">Only correlates log lines, as for <see cref="SegmentAsync"/>.</param>
        public async Task<LocationSegmentation?> SegmentLocationAsync(
            long annotationContextId,
            long locationId,
            CancellationToken cancellationToken,
            ulong requestId = 0)
        {
            if (grpcClient is null)
            {
                RecordSkip(SegmentationSkipKind.NoClient);
                return null;
            }

            if (!IsCameraWithinTileSubmission())
                return null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                (int downsample, TileSignature signature, _, bool grayscale) = await ReadViewTilesAsync().ConfigureAwait(false);
                mosaicDownsample = downsample;
                IVolumeToSectionTransform? transform = null;
                if (signature.Space == CoordinateSpace.Volume)
                {
                    transform = await Viking.UI.State.MainThreadDispatcher
                        .InvokeAsync(() => parent.Section?.ActiveSectionToVolumeTransform)
                        .Task.ConfigureAwait(false);
                    if (transform is null)
                        throw new InvalidOperationException("The section has no volume transform to draw the result with.");
                }

                Stopwatch timer = Stopwatch.StartNew();
                SegmentationResponse mosaic = await SegmentLocationStreamAsync(annotationContextId, locationId, cancellationToken).ConfigureAwait(false);
                long rpcMs = timer.ElapsedMilliseconds;

                // The server answers on the mosaic grid at downsample 1, the tile level every request uses.
                SegmentationResponse response = transform is null
                    ? mosaic
                    : await Task.Run(() => SegmentationFieldResampler.ToVolume(mosaic, transform), cancellationToken).ConfigureAwait(false);
                Polygon? reviewed = ReviewedOutline(mosaic, transform);
                SegmentationDiag.Log(
                    $"SegmentLocation location={locationId} req={requestId} cache={(mosaic.Cache is null ? "miss" : "hit")} " +
                    $"reviewed={mosaic.Reviewed?.State ?? "none"} islands={mosaic.Segments.Count}->{response.Segments.Count} " +
                    $"rpc={rpcMs}ms resample={timer.ElapsedMilliseconds - rpcMs}ms");

                resultContexts.Record(response, downsample);
                await CaptureMissingTileImagesAsync(response, signature, downsample, grayscale, cancellationToken).ConfigureAwait(false);
                AttachTileImages(response, signature, downsample);
                if (ResponseWorldBounds(response, downsample) is Geometry.Rectangle bounds)
                {
                    uploadedImageBounds = bounds;
                    uploadedImageWidth = Math.Max(1, (int)Math.Round(bounds.Width / downsample));
                    uploadedImageHeight = Math.Max(1, (int)Math.Round(bounds.Height / downsample));
                }

                ClearSkip();
                SegmentationServiceHealth.ReportSuccess();
                return new LocationSegmentation(response, reviewed);
            }
            catch (Exception ex)
            {
                RecordSegmentFailure(ex, "SegmentLocationAsync", requestId);
                return null;
            }
        }

        /// <summary>
        /// One <c>SegmentLocation</c> call: the start alone, then the request stream is closed, since this client sends no
        /// tiles. Progress messages only keep the idle timer running; the call ends with the result.
        /// </summary>
        private async Task<SegmentationResponse> SegmentLocationStreamAsync(long annotationContextId, long locationId, CancellationToken token)
        {
            if (grpcClient is null)
                throw new InvalidOperationException("Segmentation client is not connected.");

            SegmentLocationRequest start = new()
            {
                Start = new LocationStart
                {
                    AnnotationContextId = annotationContextId,
                    LocationId = locationId,
                    Options = new SegmentOptions
                    {
                        MaskThreshold = (float)WebAnnotation.Global.AnnotationSettings.SegmentationMaskThreshold,
                        CacheMode = CacheMode.Use
                    },
                    ProvidesImages = false
                }
            };

            using CancellationTokenSource idle = new();
            using CancellationTokenSource streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, idle.Token);
            void StreamAlive()
            {
                try
                {
                    idle.CancelAfter(TimeSpan.FromSeconds(SegmentStreamIdleSeconds));
                }
                catch (ObjectDisposedException)
                {
                }
            }

            StreamAlive();
            CancellationToken streamToken = streamCancellation.Token;
            CallOptions callOptions = new(
                deadline: DateTime.UtcNow.AddMinutes(SegmentStreamMaxMinutes),
                cancellationToken: streamToken);
            try
            {
                using AsyncDuplexStreamingCall<SegmentLocationRequest, SegmentLocationResponse> call = grpcClient.SegmentLocation(callOptions);
                await call.RequestStream.WriteAsync(start).ConfigureAwait(false);
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);
                while (await call.ResponseStream.MoveNext(streamToken).ConfigureAwait(false))
                {
                    StreamAlive();
                    SegmentLocationResponse update = call.ResponseStream.Current;
                    if (update.UpdateCase == SegmentLocationResponse.UpdateOneofCase.Result)
                        return update.Result;
                }
            }
            catch (Exception ex) when (idle.IsCancellationRequested && !token.IsCancellationRequested &&
                (ex is OperationCanceledException || ex is RpcException { StatusCode: StatusCode.Cancelled }))
            {
                throw new RpcException(new Status(
                    StatusCode.DeadlineExceeded,
                    $"no progress or result for {SegmentStreamIdleSeconds / 60} minutes"));
            }

            throw new InvalidOperationException("The segmentation stream ended without a result.");
        }

        /// <summary>
        /// The reviewed outline <paramref name="response"/> carries, mapped from mosaic into viewer space when the viewer draws
        /// in volume space (<paramref name="transform"/> set); null when there is none, it is not a polygon, or it does not map.
        /// </summary>
        internal static Polygon? ReviewedOutline(SegmentationResponse response, IVolumeToSectionTransform? transform)
        {
            if (response.Reviewed?.Outline?.Polygon is not Viking.gRPC.Geometry.V1.Polygon outline ||
                outline.Exterior is null || outline.Exterior.Points.Count < 3)
            {
                return null;
            }

            static Geometry.Vector2[] Ring(Viking.gRPC.Geometry.V1.Ring ring) =>
                ring.Points.Select(point => new Geometry.Vector2(point.X, point.Y)).ToArray().EnsureClosedRing();

            Polygon mosaic;
            try
            {
                mosaic = new(Ring(outline.Exterior));
                foreach (Viking.gRPC.Geometry.V1.Ring hole in outline.Holes.Where(hole => hole.Points.Count >= 3))
                    mosaic.AddInteriorRing(new Polygon(Ring(hole)));
            }
            catch (ArgumentException ex)
            {
                // A ring that crosses itself or repeats points: show the model's mask instead.
                SegmentationDiag.Log($"Reviewed outline of location {response.Reviewed.LocationId} skipped: {ex.Message}");
                return null;
            }

            return transform is null ? mosaic : LocationVolumeGeometry.TryMapPolygon(mosaic, transform);
        }
    }
}
