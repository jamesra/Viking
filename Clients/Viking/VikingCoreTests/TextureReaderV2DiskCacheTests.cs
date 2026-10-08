using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
    /// Pins the disk-first tile path of <see cref="TextureReaderV2"/>: <c>TryLoadingFromDiskOnly</c> and
    /// <c>LoadTexture(allowNetwork: false)</c>. A missing, empty, or undecodable cache file must yield null without
    /// an exception, a zero-length or undecodable file must be deleted so the next request goes to the server, and a
    /// valid image must be kept and handed to <see cref="PendingTextureQueue"/> for texture creation.
    /// </summary>
    /// <remarks>
    /// Each test works in its own temp folder, removed in cleanup. The graphics device is an uninitialized
    /// placeholder and no viewer is wired up, so a decoded tile reaches <see cref="PendingTextureQueue"/> and is
    /// completed with null when the test pumps the queue; "decoded" is observed as "queued", not as a texture.
    /// <see cref="Viking.Common.SharedResources.HttpClient"/> is replaced for the whole class so a stray server
    /// request is counted instead of leaving the machine.
    /// </remarks>
    [TestClass]
    public class TextureReaderV2DiskCacheTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

        private static readonly MethodInfo ProcessQueue =
            typeof(PendingTextureQueue).GetMethod("ProcessQueue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PendingTextureQueue), "ProcessQueue");

        private static readonly GraphicsDevice PlaceholderDevice = CreatePlaceholderDevice();

        private static readonly byte[] PngBytes = CreatePng();

        private HttpClient? _savedClient;
        private CountingHandler _handler = new();
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

        /// <summary>Answers every request with 404 and counts them.</summary>
        private sealed class CountingHandler : HttpMessageHandler
        {
            private int _requests;

            public int Requests => _requests;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _requests);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            _savedClient = Viking.Common.SharedResources.HttpClient;
            _handler = new CountingHandler();
            Viking.Common.SharedResources.HttpClient = new HttpClient(_handler);
            _folder = Path.Combine(Path.GetTempPath(), "TextureReaderV2DiskCacheTests-" + Guid.NewGuid().ToString("N"));
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

        private string NewCacheFile(byte[]? content)
        {
            string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".png");
            if (content != null)
                File.WriteAllBytes(path, content);
            return path;
        }

        private static TextureReaderV2 NewReader(string cacheFile, CancellationTokenSource cancel) =>
            new(PlaceholderDevice, new Uri($"http://tiles.test/{Guid.NewGuid():N}.png"), cacheFile, 1, null, cancel);

        /// <summary>Waits for a task that may be parked on the pending texture queue, pumping the queue like the UI thread would.</summary>
        private static T Await<T>(Task<T> task)
        {
            var deadline = DateTime.UtcNow + Patience;
            while (!task.Wait(10))
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "The load did not complete.");
                ProcessQueue.Invoke(null, null);
            }

            return task.Result;
        }

        private Texture2D? DiskOnly(string cacheFile, CancellationToken token = default)
        {
            using var cancel = new CancellationTokenSource();
            using TextureReaderV2 reader = NewReader(cacheFile, cancel);
            return Await(Task.Run(() => reader.TryLoadingFromDiskOnly(cacheFile, token)));
        }

        private Texture2D? LoadDiskOnly(string cacheFile, bool allowNetwork = false)
        {
            using var cancel = new CancellationTokenSource();
            using TextureReaderV2 reader = NewReader(cacheFile, cancel);
            return Await(Task.Run(() => reader.LoadTexture(allowNetwork)));
        }

        [TestMethod]
        public void MissingFileYieldsNullAndCreatesNothing()
        {
            string file = NewCacheFile(null);

            Assert.IsNull(DiskOnly(file));

            Assert.IsFalse(File.Exists(file));
        }

        [TestMethod]
        public void MissingDirectoryYieldsNullAndIsCreatedForTheNextWrite()
        {
            string file = Path.Combine(_folder, "not-yet", "tile.png");

            Assert.IsNull(DiskOnly(file));

            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(file)));
            Assert.IsFalse(File.Exists(file));
        }

        [TestMethod]
        public void EmptyFileNameYieldsNull()
        {
            Assert.IsNull(DiskOnly(""));
        }

        [TestMethod]
        public void ZeroByteFileIsDeletedAndYieldsNull()
        {
            string file = NewCacheFile([]);

            Assert.IsNull(DiskOnly(file));

            Assert.IsFalse(File.Exists(file));
        }

        [TestMethod]
        public void ZeroByteFileIsDeletedEvenWhenCancelled()
        {
            string file = NewCacheFile([]);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            Assert.IsNull(DiskOnly(file, cancelled.Token));

            Assert.IsFalse(File.Exists(file));
        }

        [TestMethod]
        public void NonImageBytesAreDeletedAndYieldNullWithoutThrowing()
        {
            string file = NewCacheFile([1, 2, 3, 4, 5, 6, 7, 8]);

            Assert.IsNull(DiskOnly(file));

            Assert.IsFalse(File.Exists(file));
        }

        [TestMethod]
        public void TruncatedPngHeaderIsDeletedAndYieldsNull()
        {
            string file = NewCacheFile(PngBytes.Take(8).ToArray());

            Assert.IsNull(DiskOnly(file));

            Assert.IsFalse(File.Exists(file));
        }

        [TestMethod]
        public void CancelledReadLeavesAnIntactFileAlone()
        {
            string file = NewCacheFile(PngBytes);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            using var cancel = new CancellationTokenSource();
            using TextureReaderV2 reader = NewReader(file, cancel);

            // Not pumped: a read that decoded and queued the tile would stay parked and never complete.
            Task<Texture2D?> load = Task.Run(() => reader.TryLoadingFromDiskOnly(file, cancelled.Token));

            Assert.IsTrue(load.Wait(Patience), "A cancelled read must return without waiting for the texture queue.");
            Assert.IsNull(load.Result);
            Assert.IsTrue(PendingTextureQueue.IsEmpty, "A cancelled read must not decode and queue the tile.");
            CollectionAssert.AreEqual(PngBytes, File.ReadAllBytes(file));
        }

        [TestMethod]
        public void ValidPngIsKeptAndQueuedForTextureCreation()
        {
            string file = NewCacheFile(PngBytes);
            using var cancel = new CancellationTokenSource();
            using TextureReaderV2 reader = NewReader(file, cancel);
            Task<Texture2D?> load = Task.Run(() => reader.TryLoadingFromDiskOnly(file, CancellationToken.None));

            var deadline = DateTime.UtcNow + Patience;
            while (PendingTextureQueue.IsEmpty)
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "The decoded tile never reached the pending texture queue.");
                Assert.IsFalse(load.IsCompleted, "The load finished without queueing the decoded tile.");
                Thread.Sleep(5);
            }

            Assert.IsNull(Await(load), "No viewer device, so the queue completes the tile with null.");
            CollectionAssert.AreEqual(PngBytes, File.ReadAllBytes(file));
        }

        [TestMethod]
        public void DiskOnlyLoadOfCorruptFileDeletesItWithoutAskingTheServer()
        {
            string file = NewCacheFile([9, 9, 9, 9]);

            Assert.IsNull(LoadDiskOnly(file));

            Assert.IsFalse(File.Exists(file));
            Assert.AreEqual(0, _handler.Requests);
        }

        [TestMethod]
        public void DiskOnlyLoadOfZeroByteFileDeletesItWithoutAskingTheServer()
        {
            string file = NewCacheFile([]);

            Assert.IsNull(LoadDiskOnly(file));

            Assert.IsFalse(File.Exists(file));
            Assert.AreEqual(0, _handler.Requests);
        }

        [TestMethod]
        public void DiskOnlyLoadOfMissingFileDoesNotAskTheServer()
        {
            Assert.IsNull(LoadDiskOnly(NewCacheFile(null)));

            Assert.AreEqual(0, _handler.Requests);
        }

        [TestMethod]
        public void DiskOnlyLoadOfValidPngDoesNotAskTheServerAndKeepsTheFile()
        {
            string file = NewCacheFile(PngBytes);

            Assert.IsNull(LoadDiskOnly(file));

            CollectionAssert.AreEqual(PngBytes, File.ReadAllBytes(file));
            Assert.AreEqual(0, _handler.Requests);
        }

        [TestMethod]
        public void NetworkLoadFallsBackToTheServerAfterDeletingACorruptFile()
        {
            string file = NewCacheFile([7, 7, 7, 7]);

            Assert.IsNull(LoadDiskOnly(file, allowNetwork: true));

            Assert.IsFalse(File.Exists(file));
            Assert.AreEqual(1, _handler.Requests);
        }

        /// <summary>
        /// Any non-empty run of bytes behind a marker no image decoder accepts is deleted and yields null, whatever
        /// its length: a corrupt tile never throws into the loader and never survives on disk.
        /// </summary>
        [TestMethod]
        public void AnyUndecodableFileIsDeletedAndYieldsNull()
        {
            byte[] marker = System.Text.Encoding.ASCII.GetBytes("NOT-AN-IMAGE");
            Gen<byte[]> contents =
                from length in Gen.Choose(0, 2048)
                from body in Gen.ArrayOf(length, Arb.Generate<byte>())
                select marker.Concat(body).ToArray();

            Prop.ForAll(Arb.From(contents), (byte[] bytes) =>
            {
                string file = NewCacheFile(bytes);

                Texture2D? result = DiskOnly(file);

                Assert.IsNull(result);
                Assert.IsFalse(File.Exists(file), $"A {bytes.Length}-byte undecodable file survived.");
            }).QuickCheckThrowOnFailure();
        }
    }
}
