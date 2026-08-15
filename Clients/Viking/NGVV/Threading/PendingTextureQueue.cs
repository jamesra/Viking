using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Viking.Properties;
using Viking.UI;
using Viking.UI.Controls;
using Viking.ViewModels;
using VikingXNA;
using VikingXNAWinForms;

namespace Viking
{
    /// <summary>
    /// Queue of texture data ready to be turned into Texture2D on the main thread. All creation from decoded data is intended to go through this queue (via TextureReaderV2.GetTextureFromTextureDataAsync).
    /// The main-thread pump runs while items are pending and pauses when the queue is empty; Enqueue wakes the pump.
    /// </summary>
    internal static class PendingTextureQueue
    {
        private sealed class PendingItem
        {
            internal TileView? TileView { get; }
            internal TextureData Data { get; }
            internal bool UseMipMaps { get; }
            internal TaskCompletionSource<Texture2D> Tcs { get; }
            internal string? FileKey { get; }

            internal PendingItem(TextureData data, bool useMipMaps, TaskCompletionSource<Texture2D> tcs, TileView? tileView = null, string? fileKey = null)
            {
                TileView = tileView;
                Data = data;
                UseMipMaps = useMipMaps;
                Tcs = tcs;
                FileKey = fileKey;
            }

            public override string ToString() => $"{TileView?.ToString()}";
        }

        private static readonly List<PendingItem> _items = new();
        private static readonly HashSet<TileView> PendingTileViews = new();
        private static readonly HashSet<string> _loadingFiles = new();
        /// <summary>Waiters for in-flight loads keyed by texture URL/path. Completed in EndLoadingFile.</summary>
        private static readonly Dictionary<string, TaskCompletionSource<Texture2D>> _inflightLoadsByFile = new();
        private static readonly ReaderWriterLockSlim _pendingLock = new();
        private static DispatcherTimer? _sortTimer;

        /// <summary>
        /// 1 when a ProcessQueue invocation is scheduled or running; 0 when the pump is paused.
        /// </summary>
        private static int _pumpScheduled;

        /// <summary>
        /// Claims the file for loading. Returns true if this call claimed it (caller may create TextureReaderV2); false if already loading (do not create another reader).
        /// </summary>
        public static bool TryBeginLoadingFile(string fileKey)
        {
            if (TryBeginLoadingFile(fileKey, out _))
                return true;
            return false;
        }

        /// <summary>
        /// Claims the file for loading. When false, <paramref name="inFlightLoad"/> is the task to await for the active load (if any).
        /// </summary>
        public static bool TryBeginLoadingFile(string fileKey, out Task<Texture2D> inFlightLoad)
        {
            inFlightLoad = null;
            if (string.IsNullOrEmpty(fileKey))
                return true;
            _pendingLock.EnterWriteLock();
            try
            {
                if (_loadingFiles.Contains(fileKey))
                {
                    if (_inflightLoadsByFile.TryGetValue(fileKey, out TaskCompletionSource<Texture2D> tcs))
                        inFlightLoad = tcs.Task;
                    return false;
                }
                _loadingFiles.Add(fileKey);
                _inflightLoadsByFile[fileKey] = new TaskCompletionSource<Texture2D>(TaskCreationOptions.RunContinuationsAsynchronously);
                return true;
            }
            finally
            {
                _pendingLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Releases the file claim. Call exactly once when the load completes (success, failure, or cancel).
        /// </summary>
        public static void EndLoadingFile(string fileKey, Texture2D texture = null)
        {
            if (string.IsNullOrEmpty(fileKey))
                return;
            TaskCompletionSource<Texture2D> tcs = null;
            _pendingLock.EnterWriteLock();
            try
            {
                _loadingFiles.Remove(fileKey);
                if (_inflightLoadsByFile.TryGetValue(fileKey, out tcs))
                    _inflightLoadsByFile.Remove(fileKey);
            }
            finally
            {
                _pendingLock.ExitWriteLock();
            }
            tcs?.TrySetResult(texture);
        }

        /// <summary>
        /// True if the queue has no pending texture items.
        /// </summary>
        public static bool IsEmpty
        {
            get
            {
                _pendingLock.EnterReadLock();
                try
                {
                    return _items.Count == 0;
                }
                finally
                {
                    _pendingLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Fired when the queue has just become empty (after processing the last item). Viewer can invalidate to re-request loads for visible tiles.
        /// </summary>
        public static event Action? QueueBecameEmpty;

        /// <summary>
        /// True if this TileView has a pending item in the pipeline (enqueued or dequeued but not yet completed).
        /// Used by TileView to avoid starting a duplicate load.
        /// </summary>
        public static bool IsTileViewPending(TileView tileView)
        {
            if (tileView is null)
                return false;
            _pendingLock.EnterReadLock();
            try
            {
                return PendingTileViews.Contains(tileView);
            }
            finally
            {
                _pendingLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Enqueue a pending texture creation. Call from any thread. Wakes the pump so the main thread will process.
        /// When tileView is non-null, ProcessQueue will call SetTextureFromQueue on it; when fileKey is non-null, ProcessQueue will call EndLoadingFile when the item is processed.
        /// </summary>
        public static void Enqueue(TextureData data, bool useMipMaps, TaskCompletionSource<Texture2D> tcs, TileView? tileView = null, string? fileKey = null)
        {
            if (tcs is null)
                return;

            _pendingLock.EnterWriteLock();
            try
            {
                if (tileView != null)
                    PendingTileViews.Add(tileView);
                _items.Add(new PendingItem(data, useMipMaps, tcs, tileView, fileKey));
            }
            finally
            {
                _pendingLock.ExitWriteLock();
            }

            EnsurePumpScheduled();
        }

        /// <summary>
        /// Schedule the queue pump to run on the main thread if it is not already scheduled.
        /// When the queue is empty the pump pauses until Enqueue or another PostPump wakes it.
        /// </summary>
        public static async Task PostPump(int msDelay = 0)
        {
            if (Interlocked.CompareExchange(ref _pumpScheduled, 1, 0) != 0)
                return;

            if (State.MainThreadDispatcher is null)
            {
                Interlocked.Exchange(ref _pumpScheduled, 0);
                return;
            }

            if (msDelay > 0)
                await Task.Delay(msDelay).ConfigureAwait(false);

            State.MainThreadDispatcher.BeginInvoke(new Action(ProcessQueue), priority: DispatcherPriority.Background);
        }

        /// <summary>
        /// Wake the pump immediately if it is paused. No-op if already scheduled or running.
        /// </summary>
        private static void EnsurePumpScheduled()
        {
            if (Interlocked.CompareExchange(ref _pumpScheduled, 1, 0) != 0)
                return;

            if (State.MainThreadDispatcher is null)
            {
                Interlocked.Exchange(ref _pumpScheduled, 0);
                return;
            }

            State.MainThreadDispatcher.BeginInvoke(new Action(ProcessQueue), priority: DispatcherPriority.Background);
        }

        /// <summary>
        /// Re-post ProcessQueue while the pump remains marked scheduled (items still pending).
        /// </summary>
        private static void ContinuePump()
        {
            if (State.MainThreadDispatcher is null)
            {
                Interlocked.Exchange(ref _pumpScheduled, 0);
                return;
            }

            State.MainThreadDispatcher.BeginInvoke(new Action(ProcessQueue), priority: DispatcherPriority.Background);
        }

        /// <summary>
        /// Clear the scheduled flag; if items arrived during the clear, wake the pump again.
        /// </summary>
        private static void PausePumpUnlessWorkPending()
        {
            Interlocked.Exchange(ref _pumpScheduled, 0);
            if (!IsEmpty)
                EnsurePumpScheduled();
        }

        /// <summary>
        /// Returns true when both the elapsed time and texture count thresholds
        /// have been met.  Because both conditions use AND, the larger of the
        /// two requirements is the effective limit.
        /// </summary>
        private static bool LoadingWindowClosed(int texturesLoaded, long elapsedMs)
        {
            return texturesLoaded >= Settings.Default.MinTexturesToLoadFromQueue
                && elapsedMs >= Settings.Default.TextureLoadingWindow;
        }

        /// <summary>
        /// Removes and returns the next item from the queue. Returns false if the queue is empty.
        /// Uses an upgradable read lock to check empty, then upgrades to write lock only when dequeuing.
        /// </summary>
        private static bool TryDequeue(out PendingItem? item)
        {
            item = null;
            _pendingLock.EnterUpgradeableReadLock();
            try
            {
                if (_items.Count == 0)
                    return false;
                _pendingLock.EnterWriteLock();
                try
                {
                    if (_items.Count == 0)
                        return false;
                    item = _items[0];
                    _items.RemoveAt(0);
                    return true;
                }
                finally
                {
                    _pendingLock.ExitWriteLock();
                }
            }
            finally
            {
                _pendingLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Runs on the main thread. Dequeues and creates textures until both the
        /// configured time and minimum texture count are met, then re-posts the pump if work remains.
        /// If the queue is empty the pump pauses until Enqueue wakes it.
        /// </summary>
        private static void ProcessQueue()
        {
            const int msSliceTime = 50;
            if (IsEmpty)
            {
                PausePumpUnlessWorkPending();
                return;
            }

            var sw = Stopwatch.StartNew();
            int texturesLoaded = 0;

            while (!LoadingWindowClosed(texturesLoaded, sw.ElapsedMilliseconds))
            {
                if (!TryDequeue(out PendingItem? item) || item is null)
                    break;

                if (item?.TileView?.SectionLoadingCancelled ?? false)
                {
                    //Must remove from the pending set here, otherwise Enqueue/IsTileViewPending will believe this
                    //TileView still has a request in flight and will refuse to ever queue a new load for it again.
                    _pendingLock.EnterWriteLock();
                    try
                    {
                        if (item.TileView != null)
                            PendingTileViews.Remove(item.TileView);
                    }
                    finally
                    {
                        _pendingLock.ExitWriteLock();
                    }
                    if (item.FileKey != null)
                        EndLoadingFile(item.FileKey, null);
                    item.Tcs.TrySetResult(null);
                    continue;
                }

                bool requeuedForDevice = false;
                try
                {
                    GraphicsDevice device = null;
                    var viewer = State.ViewerControl;
                    if (viewer is GraphicsDeviceControl gdc)
                        device = gdc.Device;

                    if (device is null || device.IsDisposed)
                    {
                        _pendingLock.EnterWriteLock();
                        try
                        {
                            _items.Insert(0, item);
                        }
                        finally
                        {
                            _pendingLock.ExitWriteLock();
                        }
                        requeuedForDevice = true;
                        ContinuePump();
                        return;
                    }

                    Texture2D texture = null;
                    try
                    {
                        texture = TextureReaderV2.TextureFromData(device, item.Data, item.UseMipMaps);
                    }
                    catch (Exception)
                    {
                        item.Tcs.TrySetResult(null);
                        if (item.FileKey != null)
                            EndLoadingFile(item.FileKey, null);
                        continue;
                    }

                    if (texture != null && item.TileView != null)
                        item.TileView.SetTextureFromQueue(texture);
                    item.Tcs.TrySetResult(texture);
                    texturesLoaded++;
                    if (item.FileKey != null)
                        EndLoadingFile(item.FileKey, texture);
                }
                finally
                {
                    if (!requeuedForDevice)
                    {
                        _pendingLock.EnterWriteLock();
                        try
                        {
                            if (item.TileView != null)
                                PendingTileViews.Remove(item.TileView);
                        }
                        finally
                        {
                            _pendingLock.ExitWriteLock();
                        }
                    }
                }
            }

            bool empty;
            _pendingLock.EnterReadLock();
            try
            {
                empty = _items.Count == 0;
            }
            finally
            {
                _pendingLock.ExitReadLock();
            }

            if (empty)
            {
                QueueBecameEmpty?.Invoke();
                PausePumpUnlessWorkPending();
                return;
            }

            ContinuePump();
        }

        /// <summary>
        /// Stable-sort the queue so items whose TileView is visible in the given
        /// bounds appear before non-visible items; then by Downsample (highest first);
        /// then by Z distance to current section. Called on main thread by the sort timer.
        /// </summary>
        public static void SortByVisibility(GridRectangle visibleBounds, int currentSectionZ)
        {
            _pendingLock.EnterWriteLock();
            try
            {
                if (_items.Count < TextureRequestQueue.MaxWorkers)
                    return;

                var sorted = _items
                    .OrderBy(item => item.TileView != null && item.TileView.Bounds.Intersects(visibleBounds) ? 0 : 1)
                    .ThenBy(item => Math.Abs((item.TileView?.Section ?? currentSectionZ) - currentSectionZ))
                    .ThenByDescending(item => item.TileView?.Downsample ?? 0)
                    .ToList();

                _items.Clear();
                _items.AddRange(sorted);
            }
            finally
            {
                _pendingLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Start the periodic visibility sort timer. Call once from main thread startup.
        /// </summary>
        public static void StartSortTimer()
        {
            if (_sortTimer != null) return;
            _sortTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(Settings.Default.VisibleTileSortIntervalMs)
            };
            _sortTimer.Tick += (_, _) =>
            {
                var viewer = State.ViewerControl;
                if (viewer?.Scene is null) return;
                int currentZ = (viewer as SectionViewerControl)?.Section?.Number ?? 0;
                SortByVisibility(viewer.Scene.VisibleWorldBounds, currentZ);
            };
            TextureRequestQueue.RegisterSortCallback(_sortTimer);
            _sortTimer.Start();
        }

        /// <summary>
        /// Update the sort timer interval (e.g. when the user changes the preference).
        /// </summary>
        public static void UpdateSortInterval(int intervalMs)
        {
            if (_sortTimer != null)
                _sortTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        }
         
    }
}
