using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI;
using Viking.ViewModels;
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
        }

        private static readonly ConcurrentQueue<PendingItem> Queue = new();
        private static readonly HashSet<TileView> PendingTileViews = new();
        private static readonly HashSet<string> _loadingFiles = new();
        private static readonly ReaderWriterLockSlim _pendingLock = new();
        private static DispatcherTimer _emptyQueueTimer;
        private static readonly object TimerLock = new();

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
        public static bool IsEmpty => Queue.IsEmpty;

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
            if (tileView != null)
            {
                _pendingLock.EnterWriteLock();
                try
                {
                    PendingTileViews.Add(tileView);
                }
                finally
                {
                    _pendingLock.ExitWriteLock();
                }
            }
            Queue.Enqueue(new PendingItem(data, useMipMaps, tcs, tileView, fileKey));
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
        /// Runs on the main thread. Dequeue one item, create texture via TextureFromData, assign to TileView when present, complete TCS, then post pump again.
        /// If queue is empty, re-post pump after 16ms; if not empty, after 40ms.
        /// </summary>
        private static void ProcessQueue()
        {
            if (!Queue.TryDequeue(out PendingItem item))
            {
                PostPump(50);
                return;
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
                    return;
                }

                if (texture != null && item.TileView != null)
                    item.TileView.SetTextureFromQueue(texture);
                item.Tcs.TrySetResult(texture);
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
                PostPump();
            }
        }
         
    }
}
