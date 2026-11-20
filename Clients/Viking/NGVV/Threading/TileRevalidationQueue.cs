using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Viking.Common;
using Viking.ViewModels;

namespace Viking;

/// <summary>What one revalidation of a cached tile did.</summary>
internal enum TileRevalidationOutcome
{
    /// <summary>Not checked: cancelled, the queue is paused, the host sends no validators, or the cache file is gone.</summary>
    Skipped,

    /// <summary>The server answered 304: the cached tile is current.</summary>
    Current,

    /// <summary>The server sent a different image: the cache file was replaced and the new image queued for the tile.</summary>
    Replaced,

    /// <summary>The server no longer has the tile: the cache file was deleted and the tile marked missing.</summary>
    Removed,

    /// <summary>The request failed (network, timeout, an error status) or the new image could not be decoded.</summary>
    Failed,
}

/// <summary>
/// Checks tiles drawn from the disk cache against the server, in the background, so a tile regenerated on the server
/// replaces the old copy saved on this computer. Every load of a tile from the disk cache queues one check: a GET with
/// <c>If-Modified-Since</c> set to the cache file's write time, which is the server's <c>Last-Modified</c> from when the
/// file was saved (<see cref="LocalTextureCache.AddAsync(string, Stream, DateTimeOffset?)"/>). A 304 costs one round
/// trip and no body. The disk copy is always drawn first; a check never delays drawing.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Separate from <see cref="TextureRequestQueue"/>, with <see cref="MaxWorkers"/> workers, so checks never take a
/// slot from a real load. Newest first: the tiles on screen now matter most. A check for a file already queued or running
/// is dropped, and the oldest entries go when more than <see cref="MaxQueued"/> wait.</item>
/// <item>A host whose response carries no <c>Last-Modified</c> and no <c>ETag</c> can never answer 304, so after its
/// first replacement its tiles are not checked again this session.</item>
/// <item>After <see cref="FailuresBeforePause"/> failed requests in a row (Viking offline, server down) the queue stops
/// for the rest of the session.</item>
/// </list>
/// </remarks>
internal static class TileRevalidationQueue
{
    internal const int MaxWorkers = 4;
    internal const int FailuresBeforePause = 5;
    internal const int MaxQueued = 1024;

    /// <summary>One cached tile to check.</summary>
    internal sealed class Request(Uri tileUri, string cacheFile, TileView? owner, bool useMipMaps, CancellationToken sectionToken)
    {
        public Uri TileUri { get; } = tileUri;
        public string CacheFile { get; } = cacheFile;

        /// <summary>The tile showing the cached image; gets the new image when there is one. May be null.</summary>
        public TileView? Owner { get; } = owner;
        public bool UseMipMaps { get; } = useMipMaps;

        /// <summary>Cancelled when the user leaves the section; a waiting check is then dropped.</summary>
        public CancellationToken SectionToken { get; } = sectionToken;
    }

    private static readonly object _lock = new();
    private static readonly List<Request> _waiting = [];
    private static readonly HashSet<string> _queuedOrRunning = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> _hostsWithoutValidator = new(StringComparer.OrdinalIgnoreCase);
    private static int _running;
    private static int _consecutiveFailures;
    private static int _paused;

    /// <summary>True after <see cref="FailuresBeforePause"/> failures in a row; no more checks this session.</summary>
    internal static bool IsPaused => Volatile.Read(ref _paused) != 0;

    /// <summary>
    /// Queues a check of <paramref name="request"/>'s cache file. Returns false when it is not queued (paused, cancelled,
    /// a host without validators, or the same file already queued or running).
    /// </summary>
    internal static bool Enqueue(Request request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (IsPaused || request.SectionToken.IsCancellationRequested || HostSendsNoValidator(request.TileUri))
            return false;

        lock (_lock)
        {
            if (!_queuedOrRunning.Add(request.CacheFile))
                return false;

            _waiting.Add(request);
            if (_waiting.Count > MaxQueued)
            {
                Request dropped = _waiting[0];
                _waiting.RemoveAt(0);
                _queuedOrRunning.Remove(dropped.CacheFile);
            }
        }

        TryStartWorker();
        return true;
    }

    /// <summary>True while a check of <paramref name="cacheFile"/> waits or runs.</summary>
    internal static bool IsQueuedOrRunning(string cacheFile)
    {
        lock (_lock)
            return _queuedOrRunning.Contains(cacheFile);
    }

    private static bool HostSendsNoValidator(Uri uri) => _hostsWithoutValidator.ContainsKey(uri.Authority);

    private static void TryStartWorker()
    {
        if (Interlocked.Increment(ref _running) <= MaxWorkers)
            _ = Task.Run(WorkerAsync);
        else
            Interlocked.Decrement(ref _running);
    }

    /// <summary>Takes the newest waiting request that is still wanted. When paused, drops everything waiting.</summary>
    private static bool TryTakeNewest(out Request? request)
    {
        lock (_lock)
        {
            if (IsPaused)
            {
                foreach (Request r in _waiting)
                    _queuedOrRunning.Remove(r.CacheFile);
                _waiting.Clear();
            }

            while (_waiting.Count > 0)
            {
                request = _waiting[_waiting.Count - 1];
                _waiting.RemoveAt(_waiting.Count - 1);
                if (!request.SectionToken.IsCancellationRequested)
                    return true;
                _queuedOrRunning.Remove(request.CacheFile);
            }
        }

        request = null;
        return false;
    }

    private static async Task WorkerAsync()
    {
        try
        {
            while (TryTakeNewest(out Request? request) && request is not null)
            {
                try
                {
                    await RevalidateAsync(request).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Tile revalidation failed for {request.TileUri}: {e.GetType().Name} - {e.Message}");
                }
                finally
                {
                    lock (_lock)
                        _queuedOrRunning.Remove(request.CacheFile);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _running);
            bool more;
            lock (_lock)
                more = _waiting.Count > 0;
            // A request queued while this worker was finishing found every slot taken; start a worker for it.
            if (more)
                TryStartWorker();
        }
    }

    /// <summary>
    /// Checks one cached tile against the server and acts on the answer (see <see cref="TileRevalidationOutcome"/>).
    /// Runs on the calling thread's context; the queue's workers call it, and tests call it directly.
    /// </summary>
    internal static async Task<TileRevalidationOutcome> RevalidateAsync(Request request)
    {
        CancellationToken token = request.SectionToken;
        if (token.IsCancellationRequested || IsPaused || HostSendsNoValidator(request.TileUri))
            return TileRevalidationOutcome.Skipped;

        DateTimeOffset? cachedVersion = ConditionalGet.ValidatorFor(request.CacheFile);
        if (cachedVersion is null)
            return TileRevalidationOutcome.Skipped;

        ConditionalGetResult result;
        try
        {
            result = await ConditionalGet.SendAsync(Global.HttpClient, request.TileUri, cachedVersion, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return TileRevalidationOutcome.Skipped;
        }

        using (result)
        {
            switch (result.Outcome)
            {
                case ConditionalGetOutcome.NotModified:
                    RecordSuccess();
                    return TileRevalidationOutcome.Current;

                case ConditionalGetOutcome.NotFound:
                    RecordSuccess();
                    TryDeleteCacheFile(request.CacheFile);
                    if (request.Owner is not null)
                    {
                        request.Owner.ServerTextureNotFound = true;
                        UI.State.InvalidateViewerControl();
                    }
                    return TileRevalidationOutcome.Removed;

                case ConditionalGetOutcome.Failed:
                    RecordFailure(request.TileUri, result.StatusCode);
                    return TileRevalidationOutcome.Failed;
            }

            byte[] body;
            try
            {
                body = await result.Response!.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpRequestException or IOException)
            {
                RecordFailure(request.TileUri, null);
                return TileRevalidationOutcome.Failed;
            }

            RecordSuccess();
            if (!result.HasValidator && _hostsWithoutValidator.TryAdd(request.TileUri.Authority, 0))
            {
                Trace.WriteLine($"{request.TileUri.Authority} sends no Last-Modified or ETag, so its cached tiles cannot be " +
                                "checked cheaply; they are not checked again this session.");
            }

            if (token.IsCancellationRequested)
                return TileRevalidationOutcome.Skipped;

            TextureData data;
            try
            {
                using MemoryStream decodeStream = new(body, false);
                data = TextureReaderV2.TextureDataFromStream(decodeStream);
            }
            catch (ArgumentException)
            {
                data = default;
            }

            if (data.IsEmpty)
            {
                Trace.WriteLine($"Tile revalidation: the server's new {request.TileUri} could not be decoded; keeping the cached copy.");
                return TileRevalidationOutcome.Failed;
            }

            using (MemoryStream cacheStream = new(body, false))
            {
                if (!await Global.TextureCache.AddAsync(request.CacheFile, cacheStream, result.LastModified).ConfigureAwait(false))
                    Trace.WriteLine($"Tile revalidation: could not rewrite {request.CacheFile}; the new image is shown but not saved.");
            }

            if (request.Owner is not null && !token.IsCancellationRequested)
                _ = PendingTextureQueue.EnqueueReplacement(data, request.UseMipMaps, request.Owner);

            return TileRevalidationOutcome.Replaced;
        }
    }

    private static void RecordSuccess() => Interlocked.Exchange(ref _consecutiveFailures, 0);

    private static void RecordFailure(Uri uri, HttpStatusCode? status)
    {
        if (Interlocked.Increment(ref _consecutiveFailures) >= FailuresBeforePause &&
            Interlocked.Exchange(ref _paused, 1) == 0)
        {
            Trace.WriteLine($"Tile revalidation paused for this session after {FailuresBeforePause} failures in a row; " +
                            $"last: {uri} {(status.HasValue ? ((int)status.Value).ToString() : "no response")}.");
        }
    }

    private static void TryDeleteCacheFile(string cacheFile)
    {
        try
        {
            File.Delete(cacheFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Could not delete {cacheFile}, which the server no longer has: {e.Message}");
        }
    }

    /// <summary>Clears the session state (pause, failure count, hosts without validators, waiting requests). Tests only.</summary>
    internal static void ResetForTests()
    {
        lock (_lock)
        {
            _waiting.Clear();
            _queuedOrRunning.Clear();
        }
        _hostsWithoutValidator.Clear();
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Interlocked.Exchange(ref _paused, 0);
    }
}
