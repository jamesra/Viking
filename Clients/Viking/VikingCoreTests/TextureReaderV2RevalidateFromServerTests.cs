using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="TextureReaderV2.RevalidateFromServerIfStaleAsync"/>: compares server
    /// <c>Last-Modified</c> to the on-disk cache timestamp, downloads when stale or when the header
    /// is missing, and returns null when the cache is fresh, absent, empty, or the call is cancelled.
    /// </summary>
    /// <remarks>
    /// The method is kept while decision <c>tile-cache-revalidation</c> is open; these tests document
    /// current behavior without requiring production edits. <see cref="Viking.Common.SharedResources.HttpClient"/>
    /// is replaced per test; the graphics device is an uninitialized placeholder, so a successful decode
    /// is observed by pumping <see cref="PendingTextureQueue"/> and still completes with null texture.
    /// </remarks>
    [TestClass]
    public class TextureReaderV2RevalidateFromServerTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

        private static readonly MethodInfo ProcessQueue =
            typeof(PendingTextureQueue).GetMethod("ProcessQueue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PendingTextureQueue), "ProcessQueue");

        private static readonly GraphicsDevice PlaceholderDevice = CreatePlaceholderDevice();

        private static readonly byte[] PngBytes = CreatePng();

        private HttpClient? _savedClient;
        private string _folder = "";

        private static GraphicsDevice CreatePlaceholderDevice()
        {
            var device = (GraphicsDevice)FormatterServices.GetUninitializedObject(typeof(GraphicsDevice));
            GC.SuppressFinalize(device);
            return device;
        }

        private static byte[] CreatePng()
        {
            using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
            bitmap.SetPixel(1, 2, Color.White);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        /// <summary>HTTP content that sets <see cref="Materialized"/> when the client copies the body (download path).</summary>
        private sealed class MaterializingContent(byte[] payload) : HttpContent
        {
            public bool Materialized { get; private set; }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                Materialized = true;
                return stream.WriteAsync(payload, 0, payload.Length);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = payload.Length;
                return true;
            }
        }

        /// <summary>Builds a 200 response with optional <c>Last-Modified</c> and tracks whether the body was read.</summary>
        private sealed class LastModifiedHandler : HttpMessageHandler
        {
            private int _requests;

            public int Requests => _requests;

            public MaterializingContent? LastContent { get; private set; }

            public bool BodyWasRead => LastContent?.Materialized ?? false;

            public DateTimeOffset? LastModified { get; set; }

            public byte[] Body { get; set; } = PngBytes;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _requests);
                var content = new MaterializingContent(Body);
                LastContent = content;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content,
                    RequestMessage = request,
                };
                if (LastModified.HasValue)
                    content.Headers.LastModified = LastModified;
                return Task.FromResult(response);
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            _savedClient = Viking.Common.SharedResources.HttpClient;
            _folder = Path.Combine(Path.GetTempPath(), "TextureReaderV2Revalidate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Viking.Common.SharedResources.HttpClient = _savedClient!;
            try
            {
                Directory.Delete(_folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        private string NewCacheFile(byte[] content, DateTime utcLastWrite)
        {
            string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, content);
            File.SetLastWriteTimeUtc(path, utcLastWrite);
            return path;
        }

        private static TextureReaderV2 NewReader(string cacheFile, Uri tileUri, CancellationTokenSource cancel) =>
            new(PlaceholderDevice, tileUri, cacheFile, 1, null, cancel);

        private static T Await<T>(Task<T> task)
        {
            var deadline = DateTime.UtcNow + Patience;
            while (!task.Wait(10))
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "The revalidation did not complete.");
                ProcessQueue.Invoke(null, null);
            }

            return task.Result;
        }

        private TextureReaderV2? Revalidate(
            string cacheFile,
            LastModifiedHandler handler,
            CancellationToken token = default,
            Uri? tileUri = null)
        {
            Viking.Common.SharedResources.HttpClient = new HttpClient(handler);
            using var cancel = new CancellationTokenSource();
            var reader = NewReader(cacheFile, tileUri ?? new Uri("http://tiles.test/tile.png"), cancel);
            Await(Task.Run(() => reader.RevalidateFromServerIfStaleAsync(token)));
            return reader;
        }

        [TestMethod]
        public void MissingCacheFileReturnsNullWithoutHttp()
        {
            string missing = Path.Combine(_folder, "absent.png");
            var handler = new LastModifiedHandler { LastModified = DateTimeOffset.UtcNow };

            using var cancel = new CancellationTokenSource();
            using TextureReaderV2 reader = NewReader(missing, new Uri("http://tiles.test/t.png"), cancel);
            Texture2D? result = Await(reader.RevalidateFromServerIfStaleAsync(CancellationToken.None));

            Assert.IsNull(result);
            Assert.AreEqual(0, handler.Requests);
        }

        [TestMethod]
        public void ZeroByteCacheReturnsNullWithoutHttp()
        {
            string file = NewCacheFile([], DateTime.UtcNow.AddHours(-1));
            var handler = new LastModifiedHandler { LastModified = DateTimeOffset.UtcNow };

            using var cancel = new CancellationTokenSource();
            using TextureReaderV2 reader = NewReader(file, new Uri("http://tiles.test/t.png"), cancel);
            Texture2D? result = Await(reader.RevalidateFromServerIfStaleAsync(CancellationToken.None));

            Assert.IsNull(result);
            Assert.AreEqual(0, handler.Requests);
        }

        [TestMethod]
        public void CancelledTokenReturnsNull()
        {
            string file = NewCacheFile(PngBytes, DateTime.UtcNow.AddHours(-1));
            var handler = new LastModifiedHandler { LastModified = DateTimeOffset.UtcNow };
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            using var cancel = new CancellationTokenSource();
            using TextureReaderV2 reader = NewReader(file, new Uri("http://tiles.test/t.png"), cancel);
            Texture2D? result = Await(reader.RevalidateFromServerIfStaleAsync(cancelled.Token));

            Assert.IsNull(result);
            Assert.AreEqual(0, handler.Requests);
        }

        [TestMethod]
        public void FreshCacheSkipsDownloadAndKeepsFile()
        {
            DateTime cacheTime = DateTime.UtcNow;
            string file = NewCacheFile(PngBytes, cacheTime);
            var handler = new LastModifiedHandler
            {
                LastModified = new DateTimeOffset(cacheTime.AddMinutes(-5), TimeSpan.Zero),
            };

            using (Revalidate(file, handler))
            {
            }

            Assert.AreEqual(1, handler.Requests, "Headers-only GET still runs.");
            Assert.IsFalse(handler.BodyWasRead, "A fresh cache must not read the response body.");
            CollectionAssert.AreEqual(PngBytes, File.ReadAllBytes(file));
        }

        [TestMethod]
        public void StaleCacheDownloadsBodyAndQueuesDecode()
        {
            DateTime cacheTime = DateTime.UtcNow.AddDays(-2);
            string file = NewCacheFile(PngBytes, cacheTime);
            byte[] serverBody = CreatePng();
            var handler = new LastModifiedHandler
            {
                LastModified = new DateTimeOffset(cacheTime.AddHours(1), TimeSpan.Zero),
                Body = serverBody,
            };

            using (Revalidate(file, handler))
            {
            }

            Assert.AreEqual(1, handler.Requests);
            Assert.IsTrue(handler.BodyWasRead);
            CollectionAssert.AreEqual(PngBytes, File.ReadAllBytes(file),
                "Decode completes with a placeholder device, so the disk cache is not rewritten.");
        }

        [TestMethod]
        public void MissingLastModifiedHeaderFollowsDownloadBranch()
        {
            DateTime cacheTime = DateTime.UtcNow.AddHours(-1);
            string file = NewCacheFile(PngBytes, cacheTime);
            var handler = new LastModifiedHandler { LastModified = null };

            using (Revalidate(file, handler))
            {
            }

            Assert.AreEqual(1, handler.Requests);
            Assert.IsTrue(handler.BodyWasRead);
        }

        /// <summary>
        /// Whenever server <c>Last-Modified</c> is strictly after the cache file time, the body is consumed;
        /// otherwise only headers are fetched.
        /// </summary>
        [TestMethod]
        public void StaleComparisonUsesStrictlyGreaterThanCacheWriteTime()
        {
            Prop.ForAll(
                Arb.From(Gen.Choose(-48, 48)),
                Arb.From(Gen.Choose(-48, 48)),
                (int cacheHoursAgo, int serverMinutesAfterCache) =>
                {
                    DateTime cacheTime = DateTime.UtcNow.AddHours(-Math.Abs(cacheHoursAgo) - 1);
                    string file = NewCacheFile(PngBytes, cacheTime);
                    DateTimeOffset serverModified = new(cacheTime.AddMinutes(serverMinutesAfterCache), TimeSpan.Zero);
                    var handler = new LastModifiedHandler { LastModified = serverModified };

                    using (Revalidate(file, handler))
                    {
                    }

                    bool expectDownload = serverModified.UtcDateTime > cacheTime;
                    Assert.AreEqual(expectDownload, handler.BodyWasRead,
                        $"cache={cacheTime:o}, server={serverModified:o}");
                    Assert.AreEqual(1, handler.Requests);
                }).QuickCheckThrowOnFailure();
        }
    }
}
