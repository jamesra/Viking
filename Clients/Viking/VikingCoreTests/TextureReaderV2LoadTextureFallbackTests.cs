using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins that <see cref="TextureReaderV2.LoadTexture"/> still asks the server once when reading the disk cache
    /// throws instead of returning null: the bad cache file is deleted and the tile is downloaded, exactly as for a
    /// corrupt file that <c>TryLoadingFromDiskOnly</c> rejects with null. A failure on the server path itself is not
    /// retried, and a disk-only load never reaches the network.
    /// </summary>
    /// <remarks>
    /// The escaping disk failure is a cache file the current user may not read (deny ReadData ACE): opening it throws
    /// <see cref="UnauthorizedAccessException"/>, which is not one of the exceptions <c>TryLoadingFromDiskOnly</c>
    /// turns into null, while deleting it still succeeds. <see cref="Viking.Common.SharedResources.HttpClient"/> is
    /// replaced per test by a scripted handler; the graphics device is an uninitialized placeholder, so a decoded
    /// tile is completed with null by <see cref="PendingTextureQueue"/> when the test pumps it.
    /// </remarks>
    [TestClass]
    public class TextureReaderV2LoadTextureFallbackTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

        private static readonly MethodInfo ProcessQueue =
            typeof(PendingTextureQueue).GetMethod("ProcessQueue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PendingTextureQueue), "ProcessQueue");

        private static readonly MethodInfo TryDequeue =
            typeof(PendingTextureQueue).GetMethod("TryDequeue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PendingTextureQueue), "TryDequeue");

        private static readonly GraphicsDevice PlaceholderDevice = CreatePlaceholderDevice();

        private static readonly byte[] PngBytes = CreatePng();

        private HttpClient? _savedClient;
        private string _folder = "";

        /// <summary>Server replies the test can script, in the order the reader requests them.</summary>
        public enum Reply
        {
            OkPng,
            NotFound,
            InternalServerError,
            Forbidden,
            /// <summary>429 with Retry-After: 0, retried without waiting.</summary>
            TooManyRequests,
            /// <summary>The handler throws, so the exception leaves the server path.</summary>
            Throws,
        }

        /// <summary>Hands out one scripted reply per request, repeating the last one, and counts requests.</summary>
        private sealed class ScriptedHandler(IReadOnlyList<Reply> script) : HttpMessageHandler
        {
            private int _requests;

            public int Requests => _requests;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                int index = Interlocked.Increment(ref _requests) - 1;
                Reply reply = script[Math.Min(index, script.Count - 1)];
                if (reply == Reply.Throws)
                    throw new InvalidOperationException("scripted server-path failure");

                var response = reply switch
                {
                    Reply.OkPng => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(PngBytes) },
                    Reply.NotFound => new HttpResponseMessage(HttpStatusCode.NotFound),
                    Reply.InternalServerError => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                    Reply.Forbidden => new HttpResponseMessage(HttpStatusCode.Forbidden),
                    _ => new HttpResponseMessage((HttpStatusCode)429),
                };
                if (reply == Reply.TooManyRequests)
                    response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                response.RequestMessage = request;
                return Task.FromResult(response);
            }
        }

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

        [TestInitialize]
        public void Initialize()
        {
            _savedClient = Viking.Common.SharedResources.HttpClient;
            _folder = Path.Combine(Path.GetTempPath(), "TextureReaderV2LoadTextureFallbackTests-" + Guid.NewGuid().ToString("N"));
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
            catch (UnauthorizedAccessException)
            {
            }
        }

        private string NewCacheFile(byte[]? content)
        {
            string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".png");
            if (content != null)
                File.WriteAllBytes(path, content);
            return path;
        }

        /// <summary>A valid PNG the current user may not open for reading; delete is still allowed.</summary>
        private string NewUnreadableCacheFile()
        {
            string path = NewCacheFile(PngBytes);
            FileSecurity security = File.GetAccessControl(path);
            security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,
                FileSystemRights.ReadData, AccessControlType.Deny));
            File.SetAccessControl(path, security);
            return path;
        }

        private static (int Requests, bool TextureNotFound, bool CacheFileExists) Load(string cacheFile, bool allowNetwork, params Reply[] script)
        {
            var handler = new ScriptedHandler(script);
            Viking.Common.SharedResources.HttpClient = new HttpClient(handler);

            using var cancel = new CancellationTokenSource();
            using var reader = new TextureReaderV2(PlaceholderDevice,
                new Uri($"http://tiles.test/{Guid.NewGuid():N}.png"), cacheFile, 1, null, cancel);

            Task<Texture2D> load = Task.Run(() => reader.LoadTexture(allowNetwork));
            var deadline = DateTime.UtcNow + Patience;
            while (!load.Wait(10))
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "LoadTexture did not complete.");
                ProcessQueue.Invoke(null, null);
            }

            Assert.IsNull(load.Result, "No viewer device, so a decoded tile is completed with null.");
            return (handler.Requests, reader.TextureNotFound, File.Exists(cacheFile));
        }

        [TestMethod]
        public void UnreadableCacheFileIsDeletedAndTheTileIsDownloaded()
        {
            string file = NewUnreadableCacheFile();

            var outcome = Load(file, allowNetwork: true, Reply.OkPng);

            Assert.AreEqual(1, outcome.Requests, "A disk read that throws must fall back to the server once.");
            Assert.IsFalse(outcome.CacheFileExists);
        }

        /// <summary>
        /// The queue is drained by hand and each decoded tile completed with a stand-in texture, so the downloaded
        /// texture must come back from LoadTexture and the download must be written to the cache again.
        /// </summary>
        [TestMethod]
        public void UnreadableCacheFileIsReplacedByTheDownloadedTexture()
        {
            string file = NewUnreadableCacheFile();
            var handler = new ScriptedHandler([Reply.OkPng]);
            Viking.Common.SharedResources.HttpClient = new HttpClient(handler);
            var standIn = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
            GC.SuppressFinalize(standIn);

            using var cancel = new CancellationTokenSource();
            using var reader = new TextureReaderV2(PlaceholderDevice,
                new Uri($"http://tiles.test/{Guid.NewGuid():N}.png"), file, 1, null, cancel);
            Task<Texture2D> load = Task.Run(() => reader.LoadTexture(allowNetwork: true));
            var deadline = DateTime.UtcNow + Patience;
            while (!load.Wait(10))
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "LoadTexture did not complete.");
                object?[] args = [null];
                while ((bool)TryDequeue.Invoke(null, args)!)
                {
                    var tcs = (TaskCompletionSource<Texture2D>)args[0]!.GetType()
                        .GetProperty("Tcs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(args[0])!;
                    tcs.TrySetResult(standIn);
                }
            }

            Assert.AreSame(standIn, load.Result);
            Assert.AreSame(standIn, reader.GetTexture(), "The reader must hold the downloaded texture.");
            Assert.AreEqual(1, handler.Requests);
            CollectionAssert.AreEqual(PngBytes, File.ReadAllBytes(file), "The download must replace the unreadable cache file.");
        }

        [TestMethod]
        public void UnreadableCacheFileThenNotFoundMarksTextureNotFound()
        {
            var outcome = Load(NewUnreadableCacheFile(), allowNetwork: true, Reply.NotFound);

            Assert.AreEqual(1, outcome.Requests);
            Assert.IsTrue(outcome.TextureNotFound);
        }

        [TestMethod]
        public void DiskOnlyLoadOfUnreadableCacheFileDoesNotAskTheServer()
        {
            var outcome = Load(NewUnreadableCacheFile(), allowNetwork: false, Reply.OkPng);

            Assert.AreEqual(0, outcome.Requests);
            Assert.IsFalse(outcome.CacheFileExists);
        }

        [TestMethod]
        public void FailureOnTheServerPathIsNotRetried()
        {
            var outcome = Load(NewCacheFile(null), allowNetwork: true, Reply.Throws, Reply.OkPng);

            Assert.AreEqual(1, outcome.Requests, "An exception from the download must not trigger a second download.");
        }

        /// <summary>
        /// For any server reply run (retried 429s, then a terminal reply), a cache file whose read throws ends the
        /// same way as a corrupt cache file, the path that already falls back: same request count, same
        /// TextureNotFound, and no cache file left behind.
        /// </summary>
        [TestMethod]
        public void ThrowingDiskReadFallsBackLikeACorruptCacheFile()
        {
            Reply[] terminals = [Reply.OkPng, Reply.NotFound, Reply.InternalServerError, Reply.Forbidden];
            Gen<Reply[]> scripts =
                from retries in Gen.Choose(0, 7)
                from terminal in Gen.Elements(terminals)
                select Enumerable.Repeat(Reply.TooManyRequests, retries).Append(terminal).ToArray();

            Prop.ForAll(Arb.From(scripts), (Reply[] script) =>
            {
                var reference = Load(NewCacheFile([7, 7, 7, 7]), allowNetwork: true, script);
                var unreadable = Load(NewUnreadableCacheFile(), allowNetwork: true, script);

                Assert.AreEqual(reference, unreadable, $"Replies: {string.Join(", ", script)}");
                Assert.IsTrue(unreadable.Requests > 0);
            }).QuickCheckThrowOnFailure();
        }
    }
}
