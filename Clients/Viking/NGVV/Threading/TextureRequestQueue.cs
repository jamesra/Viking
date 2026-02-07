using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI;
using Viking.UI.Controls;
using Viking.ViewModels;
using VikingXNA;

namespace Viking
{
    /// <summary>
    /// Priority-sorted queue of texture load requests. Workers pull highest-priority requests
    /// (visibility, Downsample, Z distance) so visible tiles acquire HTTP slots before others.
    /// Replaces the semaphore-based FIFO for HTTP texture requests.
    /// </summary>
    internal static class TextureRequestQueue
    {
        private sealed class RequestItem
        {
            internal TileView TileView { get; }
            internal GraphicsDevice GraphicsDevice { get; }
            internal CancellationToken SectionToken { get; }
            internal TaskCompletionSource<Texture2D> Tcs { get; }

            internal RequestItem(TileView tileView, GraphicsDevice graphicsDevice, CancellationToken sectionToken, TaskCompletionSource<Texture2D> tcs)
            {
                TileView = tileView;
                GraphicsDevice = graphicsDevice;
                SectionToken = sectionToken;
                Tcs = tcs;
            }

            public override string ToString() => TileView.ToString();
        }

        private static readonly List<RequestItem> _requests = new();
        private static readonly HashSet<TileView> _pendingTileViews = new();
        private static readonly object _lock = new();
        private static readonly SemaphoreSlim _gate = new(0, int.MaxValue);
        private static volatile SemaphoreSlim _throttle = new(DefaultMaxWorkers, DefaultMaxWorkers);
        private const int DefaultMaxWorkers = 32;
        private static readonly List<Task> _workers = new();
        private static CancellationTokenSource? _workerCts;
        private static volatile bool _started;

        /// <summary>
        /// True if this TileView has a pending request in the queue (or being processed).
        /// Used by TileView to avoid duplicate loads and by callers of PendingTextureQueue.IsTileViewPending.
        /// </summary>
        public static bool IsTileViewPending(TileView tileView)
        {
            if (tileView is null)
                return false;
            lock (_lock)
            {
                return _pendingTileViews.Contains(tileView);
            }
        }

        /// <summary>
        /// Enqueue a texture load request. Returns the Task to await. Both HTTP and local paths go through the queue.
        /// </summary>
        public static Task<Texture2D> EnqueueRequest(TileView tileView, GraphicsDevice graphicsDevice, CancellationToken sectionToken)
        {
            EnsureWorkersStarted();
            var tcs = new TaskCompletionSource<Texture2D>();
            lock (_lock)
            {
                if (_pendingTileViews.Contains(tileView))
                {
                    tcs.TrySetResult(null);
                    return tcs.Task;
                }
                _pendingTileViews.Add(tileView);
                _requests.Add(new RequestItem(tileView, graphicsDevice, sectionToken, tcs));
                _gate.Release(1);
            }
            return tcs.Task;
        }

        /// <summary>
        /// Dequeue the highest-priority request. Called by workers. Returns null if queue is empty.
        /// </summary>
        private static RequestItem? TryDequeueNext()
        {
            lock (_lock)
            {
                RequestItem? item = null;
                while(item is null)
                { 
                    if (_requests.Count == 0)
                        return null;
                    item = _requests[0];
                    _requests.RemoveAt(0);

                    if(item.TileView.SectionLoadingCancelled)
                    {
                        Trace.WriteLine($"{item.TileView} cancelled and dropped from Queue");
                        item = null;
                        continue; 
                    } 
                }
                return item;
            }
        }

        /// <summary>
        /// Stable-sort the queue by visibility, then Downsample (highest first), then Z distance.
        /// Same keys as PendingTextureQueue.SortByVisibility. Called from the sort timer.
        /// </summary>
        public static void SortByPriority(GridRectangle visibleBounds, int currentSectionZ)
        {
            lock (_lock)
            {
                if (_requests.Count < 2)
                    return;
                var sorted = _requests
                    .OrderBy(r => r.TileView.Bounds.Intersects(visibleBounds) ? 0 : 1)
                    .ThenBy(r => Math.Abs(r.TileView.Section - currentSectionZ))
                    .ThenByDescending(r => r.TileView.Downsample)
                    .ToList();
                _requests.Clear();
                _requests.AddRange(sorted);
            }
        }

        /// <summary>
        /// Set the max concurrent texture load workers (1-256). Replaces HttpRequestThrottle sizing.
        /// </summary>
        public static void SetMaxWorkers(int max)
        {
            int clamped = Math.Max(1, Math.Min(max, 256));
            var prev = _throttle;
            _throttle = new SemaphoreSlim(clamped, clamped);
            Trace.WriteLine($"TextureRequestQueue: Max workers set to {clamped}");
        }

        /// <summary>
        /// Start the worker pool. Call once from main thread startup, or lazily on first enqueue.
        /// </summary>
        public static void StartWorkers()
        {
            EnsureWorkersStarted();
        }

        private static void EnsureWorkersStarted()
        {
            if (_started)
                return;
            lock (_lock)
            {
                if (_started)
                    return;
                _started = true;
                _workerCts = new CancellationTokenSource();
                for (int i = 0; i < DefaultMaxWorkers; i++)
                {
                    _workers.Add(Task.Run(() => WorkerLoop(_workerCts.Token)));
                }
            }
        }

        private static async Task WorkerLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _gate.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var item = TryDequeueNext();
                if (item is null)
                    continue;

                var throttle = _throttle;
                try
                {
                    await throttle.WaitAsync(item.SectionToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    CompleteRequest(item, null);
                    continue;
                }

                try
                {
                    await ProcessRequest(item).ConfigureAwait(false);
                }
                finally
                {
                    throttle.Release();
                }
            }
        }

        private static void CompleteRequest(RequestItem item, Texture2D? texture)
        {
            lock (_lock)
            {
                _pendingTileViews.Remove(item.TileView);
            }
            item.Tcs.TrySetResult(texture);
        }

        private static async Task ProcessRequest(RequestItem item)
        {
            if (item.SectionToken.IsCancellationRequested)
            {
                CompleteRequest(item, null);
                return;
            }

            if (!PendingTextureQueue.TryBeginLoadingFile(item.TileView.TextureFileName))
            {
                CompleteRequest(item, null);
                return;
            }

            var volume = UI.State.volume;
            if (volume is null)
            {
                PendingTextureQueue.EndLoadingFile(item.TileView.TextureFileName);
                CompleteRequest(item, null);
                return;
            }

            using var cts = item.SectionToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(item.SectionToken)
                : new CancellationTokenSource();
            try
            {
                TextureReaderV2 texReader = volume.IsLocal == false
                    ? new TextureReaderV2(item.GraphicsDevice,
                                          new Uri(item.TileView.TextureFileName),
                                          item.TileView.TextureCachedFileName,
                                          item.TileView.MipMapLevelsForLoad,
                                          null,
                                          cts,
                                          tileViewOwner: item.TileView,
                                          sectionToken: item.SectionToken)
                    : new TextureReaderV2(item.GraphicsDevice,
                                          new Uri(item.TileView.TextureFileName),
                                          item.TileView.MipMapLevelsForLoad,
                                          null,
                                          cts,
                                          tileViewOwner: item.TileView,
                                          sectionToken: item.SectionToken);

                var texture = await texReader.LoadTexture().ConfigureAwait(false);
                item.TileView.ServerTextureNotFound = texReader.TextureNotFound;
                CompleteRequest(item, texture);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TextureRequestQueue load failed for {item.TileView.TextureFileName}: {ex.Message}");
                CompleteRequest(item, null);
            }
            finally
            {
                PendingTextureQueue.EndLoadingFile(item.TileView.TextureFileName);
            }
        }

        /// <summary>
        /// Extend the sort timer to also call SortByPriority. Called from PendingTextureQueue.StartSortTimer.
        /// </summary>
        public static void RegisterSortCallback(DispatcherTimer sortTimer)
        {
            if (sortTimer == null)
                return;
            sortTimer.Tick += (_, _) =>
            {
                var viewer = UI.State.ViewerControl;
                if (viewer?.Scene is null) return;
                int currentZ = (viewer as SectionViewerControl)?.Section?.Number ?? 0;
                SortByPriority(viewer.Scene.VisibleWorldBounds, currentZ);
            };
        }
    }
}
