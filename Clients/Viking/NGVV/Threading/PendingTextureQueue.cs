using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using SharpDX.Direct3D9;
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
    /// The main thread pump processes one item at a time; when the queue is empty a 16ms timer re-posts the pump, otherwise 40ms.
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
        private static readonly ReaderWriterLockSlim _pendingLock = new();
        private static DispatcherTimer? _emptyQueueTimer;
        private static readonly object TimerLock = new();
        private static DispatcherTimer? _sortTimer;

        /// <summary>
        /// Claims the file for loading. Returns true if this call claimed it (caller may create TextureReaderV2); false if already loading (do not create another reader).
        /// </summary>
        public static bool TryBeginLoadingFile(string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                return true;
            _pendingLock.EnterWriteLock();
            try
            {
                if (_loadingFiles.Contains(fileKey))
                    return false;
                _loadingFiles.Add(fileKey);
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
        public static void EndLoadingFile(string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                return;
            _pendingLock.EnterWriteLock();
            try
            {
                _loadingFiles.Remove(fileKey);
            }
            finally
            {
                _pendingLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// True if the queue has no pending texture items. Used to tune screen refresh interval (e.g. 16ms when empty, 40ms when busy).
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
        /// Enqueue a pending texture creation. Call from any thread. Posts the pump so the main thread will process.
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
        }

        /// <summary>
        /// Schedule the queue pump to run on the main thread.
        /// </summary>
        public static async Task PostPump(int msDelay=0)
        {
            if (State.MainThreadDispatcher is null)
                return;
            if(msDelay > 0)
                await Task.Delay(msDelay).ConfigureAwait(false);

            State.MainThreadDispatcher.BeginInvoke(new Action(ProcessQueue), priority: DispatcherPriority.Background);
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
        /// configured time and minimum texture count are met, then re-posts the pump.
        /// If the queue is empty on entry the pump is re-posted after a 50ms delay.
        /// </summary>
        private static void ProcessQueue()
        {
            const int msSliceTime = 50;
            if (PendingTextureQueue.IsEmpty)
            {
                PostPump(msSliceTime);
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
                    item.Tcs.TrySetResult(null);
                    continue;
                }

                try
                {
                    GraphicsDevice device = null;
                    var viewer = State.ViewerControl;
                    if (viewer is GraphicsDeviceControl gdc)
                        device = gdc.Device;

                    if (device is null || device.IsDisposed)
                    {
                        item.Tcs.TrySetResult(null);
                        break;
                    }

                    Texture2D texture = null;
                    try
                    {
                        texture = TextureReaderV2.TextureFromData(device, item.Data, item.UseMipMaps);
                    }
                    catch (Exception)
                    {
                        item.Tcs.TrySetResult(null);
                        continue;
                    }

                    if (texture != null && item.TileView != null)
                        item.TileView.SetTextureFromQueue(texture);
                    item.Tcs.TrySetResult(texture);
                    texturesLoaded++;
                }
                finally
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
                    if (item.FileKey != null)
                        EndLoadingFile(item.FileKey);
                }
            }

            _pendingLock.EnterReadLock();
            try
            {
                if (_items.Count == 0)
                    QueueBecameEmpty?.Invoke();
            }
            finally
            {
                _pendingLock.ExitReadLock();
            }
            PostPump();
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
                if (_items.Count < 2)
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
