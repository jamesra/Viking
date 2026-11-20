using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking;
using Viking.ViewModels;
using Viking.VolumeModel;
using Path = System.IO.Path;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="TileRevalidationQueue"/>: a cached tile is checked with <c>If-Modified-Since</c> set to its cache
    /// file's time; 304 leaves it alone, 200 replaces the file (stamped with the server's date) and queues the new image,
    /// 404 removes it; a host without validators is checked once; repeated failures pause the queue; and a replacement
    /// never disturbs the texture queue's load bookkeeping.
    /// </summary>
    /// <remarks>
    /// <see cref="Viking.Common.SharedResources.HttpClient"/> is replaced per test. There is no graphics device, so a
    /// queued replacement completes with a null texture when the queue is pumped.
    /// </remarks>
    [TestClass]
    public class TileRevalidationQueueTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

        private static readonly MethodInfo ProcessQueue =
            typeof(PendingTextureQueue).GetMethod("ProcessQueue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PendingTextureQueue), "ProcessQueue");

        private static readonly byte[] OldPng = CreatePng(Color.Black);
        private static readonly byte[] NewPng = CreatePng(Color.White);
        private static readonly DateTime CachedServerDate = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTimeOffset RebuiltServerDate = new(2026, 11, 5, 19, 6, 38, TimeSpan.Zero);

        private HttpClient? _savedClient;
        private string _folder = "";

        private static byte[] CreatePng(Color pixel)
        {
            using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
            bitmap.SetPixel(1, 2, pixel);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        /// <summary>
        /// Plays a server: 304 when <c>If-Modified-Since</c> equals <see cref="LastModified"/> exactly (nginx's rule),
        /// otherwise <see cref="Status"/> with <see cref="Body"/>. Counts requests; can hold them until released.
        /// </summary>
        private sealed class TileServer : HttpMessageHandler
        {
            private int _requests;
            public int Requests => Volatile.Read(ref _requests);
            public DateTimeOffset? LastIfModifiedSince { get; private set; }
            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
            public DateTimeOffset? LastModified { get; set; }
            public byte[] Body { get; set; } = NewPng;
            public ManualResetEventSlim Gate { get; } = new(true);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _requests);
                LastIfModifiedSince = request.Headers.IfModifiedSince;
                await Task.Run(() => Gate.Wait(cancellationToken), cancellationToken).ConfigureAwait(false);

                HttpStatusCode status = Status;
                if (status == HttpStatusCode.OK && LastModified.HasValue && request.Headers.IfModifiedSince == LastModified)
                    status = HttpStatusCode.NotModified;

                var response = new HttpResponseMessage(status) { RequestMessage = request, Content = new ByteArrayContent(status == HttpStatusCode.OK ? Body : []) };
                if (status == HttpStatusCode.OK)
                    response.Content.Headers.LastModified = LastModified;
                return response;
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            _savedClient = Viking.Common.SharedResources.HttpClient;
            _folder = Path.Combine(Path.GetTempPath(), "TileRevalidation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            TileRevalidationQueue.ResetForTests();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Viking.Common.SharedResources.HttpClient = _savedClient!;
            TileRevalidationQueue.ResetForTests();
            Drain();
            try
            {
                Directory.Delete(_folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private TileServer Serve(TileServer server)
        {
            Viking.Common.SharedResources.HttpClient = new HttpClient(server);
            return server;
        }

        private string NewCacheFile(byte[] content, DateTime utcWrite)
        {
            string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, content);
            File.SetLastWriteTimeUtc(path, utcWrite);
            return path;
        }

        private static Uri NewTileUri() => new($"http://tiles-{Guid.NewGuid():N}.test/RC2/TEM/1240/128/X001_Y001.png");

        private static TileView CreateTile(string name)
        {
            PositionNormalTextureVertex[] verticies =
            [
                new(new Vector3(0, 0, 0), Vector3.UnitZ, new Vector2(0, 0)),
                new(new Vector3(256, 0, 0), Vector3.UnitZ, new Vector2(1, 0)),
                new(new Vector3(256, 256, 0), Vector3.UnitZ, new Vector2(1, 1)),
                new(new Vector3(0, 256, 0), Vector3.UnitZ, new Vector2(0, 1)),
            ];
            string url = $"http://tiles.test/{name}.png";
            var key = TileUniqueKey.Create(1240, "Grid", "TEM", 128, name);
            var model = new TileViewModel(key, verticies, [0, 1, 3, 3, 1, 2], url, name + ".png", 1);
            return new TileView(model, url, name + ".png", 1, model.Size, "Grid");
        }

        private static TileRevalidationQueue.Request NewRequest(Uri uri, string cacheFile, TileView? owner = null, CancellationToken token = default) =>
            new(uri, cacheFile, owner, useMipMaps: false, token);

        private static TileRevalidationOutcome Revalidate(TileRevalidationQueue.Request request) =>
            Task.Run(() => TileRevalidationQueue.RevalidateAsync(request)).Result;

        private static void Drain()
        {
            for (int pass = 0; pass < 1000 && !PendingTextureQueue.IsEmpty; pass++)
                ProcessQueue.Invoke(null, null);
        }

        private static void WaitUntil(Func<bool> condition, string what)
        {
            DateTime deadline = DateTime.UtcNow + Patience;
            while (!condition())
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, $"Timed out waiting for {what}.");
                Thread.Sleep(10);
            }
        }

        [TestMethod]
        public void UnchangedTileGets304AndIsLeftAlone()
        {
            TileServer server = Serve(new TileServer { LastModified = new DateTimeOffset(CachedServerDate) });
            string file = NewCacheFile(OldPng, CachedServerDate);

            Assert.AreEqual(TileRevalidationOutcome.Current, Revalidate(NewRequest(NewTileUri(), file)));

            Assert.AreEqual(1, server.Requests);
            Assert.AreEqual(new DateTimeOffset(CachedServerDate), server.LastIfModifiedSince, "Sends the cache file's time.");
            CollectionAssert.AreEqual(OldPng, File.ReadAllBytes(file));
            Assert.AreEqual(CachedServerDate, File.GetLastWriteTimeUtc(file));
            Assert.IsTrue(PendingTextureQueue.IsEmpty, "Nothing is queued for the tile.");
        }

        [TestMethod]
        public void RebuiltTileReplacesTheCacheFileStampedWithTheServersDate()
        {
            Serve(new TileServer { LastModified = RebuiltServerDate, Body = NewPng });
            string file = NewCacheFile(OldPng, CachedServerDate);
            TileView tile = CreateTile("rebuilt-" + Guid.NewGuid().ToString("N"));

            Assert.AreEqual(TileRevalidationOutcome.Replaced, Revalidate(NewRequest(NewTileUri(), file, tile)));

            CollectionAssert.AreEqual(NewPng, File.ReadAllBytes(file));
            Assert.AreEqual(RebuiltServerDate.UtcDateTime, File.GetLastWriteTimeUtc(file));
            Assert.IsFalse(PendingTextureQueue.IsEmpty, "The new image is queued for the tile.");
            Assert.IsFalse(PendingTextureQueue.IsTileViewPending(tile), "A replacement does not mark the tile as loading.");
            Drain();
            Assert.IsFalse(PendingTextureQueue.IsTileViewPending(tile));
        }

        /// <summary>A cache file stamped by an older Viking holds its download time; nginx answers 200 and the file is re-stamped.</summary>
        [TestMethod]
        public void CacheFileWithADownloadTimeIsDownloadedOnceThenCurrent()
        {
            TileServer server = Serve(new TileServer { LastModified = new DateTimeOffset(CachedServerDate), Body = OldPng });
            string file = NewCacheFile(OldPng, CachedServerDate.AddDays(3));
            Uri uri = NewTileUri();

            Assert.AreEqual(TileRevalidationOutcome.Replaced, Revalidate(NewRequest(uri, file)));
            Assert.AreEqual(TileRevalidationOutcome.Current, Revalidate(NewRequest(uri, file)));

            Assert.AreEqual(2, server.Requests);
            Assert.AreEqual(CachedServerDate, File.GetLastWriteTimeUtc(file));
        }

        [TestMethod]
        public void TileTheServerNoLongerHasIsRemoved()
        {
            Serve(new TileServer { Status = HttpStatusCode.NotFound });
            string file = NewCacheFile(OldPng, CachedServerDate);
            TileView tile = CreateTile("gone-" + Guid.NewGuid().ToString("N"));

            Assert.AreEqual(TileRevalidationOutcome.Removed, Revalidate(NewRequest(NewTileUri(), file, tile)));

            Assert.IsFalse(File.Exists(file));
            Assert.IsTrue(tile.ServerTextureNotFound);
        }

        [TestMethod]
        public void UndecodableNewImageKeepsTheCachedCopy()
        {
            Serve(new TileServer { LastModified = RebuiltServerDate, Body = [1, 2, 3, 4] });
            string file = NewCacheFile(OldPng, CachedServerDate);

            Assert.AreEqual(TileRevalidationOutcome.Failed, Revalidate(NewRequest(NewTileUri(), file)));

            CollectionAssert.AreEqual(OldPng, File.ReadAllBytes(file));
            Assert.AreEqual(CachedServerDate, File.GetLastWriteTimeUtc(file));
            Assert.IsFalse(TileRevalidationQueue.IsPaused, "A bad image is not a network failure.");
        }

        [TestMethod]
        public void HostWithoutValidatorsIsCheckedOnceThenSkipped()
        {
            TileServer server = Serve(new TileServer { LastModified = null, Body = NewPng });
            Uri uri = NewTileUri();
            string first = NewCacheFile(OldPng, CachedServerDate);
            string second = NewCacheFile(OldPng, CachedServerDate);

            Assert.AreEqual(TileRevalidationOutcome.Replaced, Revalidate(NewRequest(uri, first)));
            Assert.AreEqual(TileRevalidationOutcome.Skipped, Revalidate(NewRequest(new Uri(uri, "X002_Y001.png"), second)));
            Assert.IsFalse(TileRevalidationQueue.Enqueue(NewRequest(uri, second)));

            Assert.AreEqual(1, server.Requests);
            Assert.AreEqual(TileRevalidationOutcome.Current,
                Task.Run(() => { Serve(new TileServer { LastModified = new DateTimeOffset(CachedServerDate) }); return TileRevalidationQueue.RevalidateAsync(NewRequest(NewTileUri(), second)); }).Result,
                "Other hosts are still checked.");
        }

        [TestMethod]
        public void RepeatedFailuresPauseTheQueueForTheSession()
        {
            TileServer server = Serve(new TileServer { Status = HttpStatusCode.ServiceUnavailable });
            string file = NewCacheFile(OldPng, CachedServerDate);

            for (int i = 0; i < TileRevalidationQueue.FailuresBeforePause; i++)
                Assert.AreEqual(TileRevalidationOutcome.Failed, Revalidate(NewRequest(NewTileUri(), file)));

            Assert.IsTrue(TileRevalidationQueue.IsPaused);
            Assert.AreEqual(TileRevalidationOutcome.Skipped, Revalidate(NewRequest(NewTileUri(), file)));
            Assert.IsFalse(TileRevalidationQueue.Enqueue(NewRequest(NewTileUri(), file)));
            Assert.AreEqual(TileRevalidationQueue.FailuresBeforePause, server.Requests);
            CollectionAssert.AreEqual(OldPng, File.ReadAllBytes(file));
        }

        [TestMethod]
        public void ASuccessResetsTheFailureCount()
        {
            TileServer server = Serve(new TileServer { Status = HttpStatusCode.ServiceUnavailable, LastModified = new DateTimeOffset(CachedServerDate) });
            string file = NewCacheFile(OldPng, CachedServerDate);

            for (int i = 0; i < TileRevalidationQueue.FailuresBeforePause - 1; i++)
                Revalidate(NewRequest(NewTileUri(), file));
            server.Status = HttpStatusCode.OK;
            Assert.AreEqual(TileRevalidationOutcome.Current, Revalidate(NewRequest(NewTileUri(), file)));
            server.Status = HttpStatusCode.ServiceUnavailable;
            Revalidate(NewRequest(NewTileUri(), file));

            Assert.IsFalse(TileRevalidationQueue.IsPaused);
        }

        [TestMethod]
        public void MissingCacheFileOrCancelledSectionSendsNothing()
        {
            TileServer server = Serve(new TileServer());
            using var left = new CancellationTokenSource();
            left.Cancel();

            Assert.AreEqual(TileRevalidationOutcome.Skipped, Revalidate(NewRequest(NewTileUri(), Path.Combine(_folder, "absent.png"))));
            Assert.AreEqual(TileRevalidationOutcome.Skipped, Revalidate(NewRequest(NewTileUri(), NewCacheFile(OldPng, CachedServerDate), token: left.Token)));
            Assert.IsFalse(TileRevalidationQueue.Enqueue(NewRequest(NewTileUri(), NewCacheFile(OldPng, CachedServerDate), token: left.Token)));

            Assert.AreEqual(0, server.Requests);
        }

        /// <summary>Every load of a tile is checked; a second load while the first check is still waiting or running is dropped.</summary>
        [TestMethod]
        public void EveryLoadIsCheckedButNotTwiceAtOnce()
        {
            TileServer server = Serve(new TileServer { LastModified = new DateTimeOffset(CachedServerDate) });
            server.Gate.Reset();
            string file = NewCacheFile(OldPng, CachedServerDate);
            Uri uri = NewTileUri();

            Assert.IsTrue(TileRevalidationQueue.Enqueue(NewRequest(uri, file)));
            WaitUntil(() => server.Requests == 1, "the first check to start");
            Assert.IsFalse(TileRevalidationQueue.Enqueue(NewRequest(uri, file)), "The same file is already being checked.");

            server.Gate.Set();
            WaitUntil(() => !TileRevalidationQueue.IsQueuedOrRunning(file), "the first check to finish");
            Assert.IsTrue(TileRevalidationQueue.Enqueue(NewRequest(uri, file)), "A later load is checked again.");
            WaitUntil(() => !TileRevalidationQueue.IsQueuedOrRunning(file), "the second check to finish");

            Assert.AreEqual(2, server.Requests);
        }

        /// <summary>A replacement that arrives after its tile stopped showing a texture is dropped without touching the queue's bookkeeping.</summary>
        [TestMethod]
        public void ReplacementForATileWithoutTextureIsDropped()
        {
            Drain();
            TileView tile = CreateTile("freed-" + Guid.NewGuid().ToString("N"));
            Assert.IsTrue(PendingTextureQueue.TryBeginLoadingFile(tile.TextureFileName), "Another load holds the file claim.");
            try
            {
                using MemoryStream png = new(NewPng, false);
                Task<Texture2D> shown = PendingTextureQueue.EnqueueReplacement(TextureReaderV2.TextureDataFromStream(png), false, tile);
                tile.FreeTexture();

                Drain();

                Assert.IsTrue(shown.IsCompleted);
                Assert.IsNull(shown.Result);
                Assert.IsFalse(PendingTextureQueue.IsTileViewPending(tile));
                Assert.IsFalse(PendingTextureQueue.TryBeginLoadingFile(tile.TextureFileName), "The replacement must not release another load's claim.");
                Assert.IsFalse(tile.TryReplaceTextureFromQueue(null!), "A tile with no texture takes no replacement.");
            }
            finally
            {
                PendingTextureQueue.EndLoadingFile(tile.TextureFileName);
            }
        }

        [TestMethod]
        public void CacheAddStampsTheFileWithTheServersDate()
        {
            string file = Path.Combine(_folder, "sub", "tile.png");
            using MemoryStream png = new(NewPng, false);

            Assert.IsTrue(Viking.Global.TextureCache.AddAsync(file, png, RebuiltServerDate).Result);

            Assert.AreEqual(RebuiltServerDate.UtcDateTime, File.GetLastWriteTimeUtc(file));
            CollectionAssert.AreEqual(NewPng, File.ReadAllBytes(file));
        }
    }
}
