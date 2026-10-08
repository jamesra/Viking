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
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins that <see cref="TextureReaderV2.LoadTexture"/> disposes every <see cref="HttpResponseMessage"/> it
    /// receives from the shared <see cref="HttpClient"/>: success (decoded or not), 404 and other error statuses,
    /// each retried 429, and an exhausted retry run. It also pins the retry count and when
    /// <see cref="TextureReaderV2.TextureNotFound"/> is set, so the disposal fix leaves that behavior alone.
    /// </summary>
    /// <remarks>
    /// <see cref="Viking.Common.SharedResources.HttpClient"/> is swapped for a client over <see cref="ScriptedHandler"/>
    /// and restored after each test. The reader has no cache file name, so the disk cache is never read or written,
    /// and the graphics device is an uninitialized placeholder: the decode path only checks it for null, and
    /// <see cref="PendingTextureQueue"/> completes decoded tiles with null because no viewer is wired up.
    /// Most retries use 429 with <c>Retry-After: 0</c> so they do not wait; 503 and 408 wait the ~1 s jitter, so only
    /// one example uses them.
    /// </remarks>
    [TestClass]
    public class TextureReaderV2ResponseDisposalTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

        private static readonly MethodInfo ProcessQueue =
            typeof(PendingTextureQueue).GetMethod("ProcessQueue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PendingTextureQueue), "ProcessQueue");

        private static readonly GraphicsDevice PlaceholderDevice = CreatePlaceholderDevice();

        private HttpClient? _savedClient;

        private static GraphicsDevice CreatePlaceholderDevice()
        {
            var device = (GraphicsDevice)FormatterServices.GetUninitializedObject(typeof(GraphicsDevice));
            GC.SuppressFinalize(device);
            return device;
        }

        [TestInitialize]
        public void Initialize() => _savedClient = Viking.Common.SharedResources.HttpClient;

        [TestCleanup]
        public void Cleanup() => Viking.Common.SharedResources.HttpClient = _savedClient!;

        /// <summary>Server replies the test can script, in the order the reader requests them.</summary>
        public enum Reply
        {
            /// <summary>200 with a body that is not an image (decode throws ArgumentException).</summary>
            OkNotAnImage,
            /// <summary>200 with a valid PNG (decoded, then dropped by the queue without a device).</summary>
            OkPng,
            NotFound,
            InternalServerError,
            Forbidden,
            /// <summary>429 with Retry-After: 0, retried without waiting.</summary>
            TooManyRequests,
            /// <summary>429 with Retry-After: 1 s.</summary>
            TooManyRequestsWaitOneSecond,
            /// <summary>503 with Retry-After: 30 s, which the reader ignores (it waits its ~1 s jitter).</summary>
            ServiceUnavailable,
            /// <summary>408, retried after the ~1 s jitter.</summary>
            RequestTimeout,
        }

        private sealed class TrackedResponse(HttpStatusCode status) : HttpResponseMessage(status)
        {
            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>Hands out one scripted response per request and keeps every response it created.</summary>
        private sealed class ScriptedHandler(IReadOnlyList<Reply> script) : HttpMessageHandler
        {
            public List<TrackedResponse> Responses { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Reply reply = script[Math.Min(Responses.Count, script.Count - 1)];
                TrackedResponse response = reply switch
                {
                    Reply.OkNotAnImage => new TrackedResponse(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4]) },
                    Reply.OkPng => new TrackedResponse(HttpStatusCode.OK) { Content = new ByteArrayContent(PngBytes) },
                    Reply.NotFound => new TrackedResponse(HttpStatusCode.NotFound) { Content = new StringContent("missing") },
                    Reply.InternalServerError => new TrackedResponse(HttpStatusCode.InternalServerError) { Content = new StringContent("error") },
                    Reply.Forbidden => new TrackedResponse(HttpStatusCode.Forbidden) { Content = new StringContent("forbidden") },
                    Reply.ServiceUnavailable => new TrackedResponse(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy") },
                    Reply.RequestTimeout => new TrackedResponse(HttpStatusCode.RequestTimeout) { Content = new StringContent("timeout") },
                    _ => new TrackedResponse((HttpStatusCode)429) { Content = new StringContent("slow down") },
                };
                TimeSpan? retryAfter = reply switch
                {
                    Reply.TooManyRequests => TimeSpan.Zero,
                    Reply.TooManyRequestsWaitOneSecond => TimeSpan.FromSeconds(1),
                    Reply.ServiceUnavailable => TimeSpan.FromSeconds(30),
                    _ => null,
                };
                if (retryAfter.HasValue)
                    response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter.Value);
                response.RequestMessage = request;
                Responses.Add(response);
                return Task.FromResult<HttpResponseMessage>(response);
            }
        }

        private static readonly byte[] PngBytes = CreatePng();

        private static byte[] CreatePng()
        {
            using var bitmap = new Bitmap(4, 4, PixelFormat.Format24bppRgb);
            bitmap.SetPixel(1, 2, Color.White);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        private sealed class Outcome(List<TrackedResponse> responses, bool textureNotFound, bool hasTexture)
        {
            public List<TrackedResponse> Responses { get; } = responses;
            public bool TextureNotFound { get; } = textureNotFound;
            public bool HasTexture { get; } = hasTexture;
        }

        private static Outcome Load(params Reply[] script)
        {
            var handler = new ScriptedHandler(script);
            Viking.Common.SharedResources.HttpClient = new HttpClient(handler);

            using var cancel = new CancellationTokenSource();
            using var reader = new TextureReaderV2(PlaceholderDevice,
                new Uri($"http://tiles.test/{Guid.NewGuid():N}.png"), 1, null, cancel);

            Task<Texture2D> load = Task.Run(() => reader.LoadTexture());
            var deadline = DateTime.UtcNow + Patience;
            while (!load.Wait(10))
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "LoadTexture did not complete.");
                ProcessQueue.Invoke(null, null);
            }

            Assert.IsNull(load.Result);
            return new Outcome(handler.Responses, reader.TextureNotFound, reader.HasTexture);
        }

        private static void AssertAllDisposed(Outcome outcome)
        {
            for (int i = 0; i < outcome.Responses.Count; i++)
                Assert.IsTrue(outcome.Responses[i].IsDisposed,
                    $"Response {i + 1} of {outcome.Responses.Count} ({(int)outcome.Responses[i].StatusCode}) was not disposed.");
        }

        [TestMethod]
        public void NotFoundResponseIsDisposedAndMarksTextureNotFound()
        {
            Outcome outcome = Load(Reply.NotFound);

            Assert.AreEqual(1, outcome.Responses.Count);
            AssertAllDisposed(outcome);
            Assert.IsTrue(outcome.TextureNotFound);
        }

        [TestMethod]
        public void ErrorStatusResponseIsDisposedWithoutRetry()
        {
            Outcome outcome = Load(Reply.InternalServerError);

            Assert.AreEqual(1, outcome.Responses.Count);
            AssertAllDisposed(outcome);
            Assert.IsFalse(outcome.TextureNotFound);
        }

        [TestMethod]
        public void SuccessResponseWithUndecodableBodyIsDisposed()
        {
            Outcome outcome = Load(Reply.OkNotAnImage);

            Assert.AreEqual(1, outcome.Responses.Count);
            AssertAllDisposed(outcome);
            Assert.IsFalse(outcome.TextureNotFound);
        }

        [TestMethod]
        public void SuccessResponseIsDisposedAfterDecode()
        {
            Outcome outcome = Load(Reply.OkPng);

            Assert.AreEqual(1, outcome.Responses.Count);
            AssertAllDisposed(outcome);
            Assert.IsFalse(outcome.HasTexture, "No viewer device, so the queue completes the decoded tile with null.");
        }

        [TestMethod]
        public void RetriedResponsesAndTheFinalNotFoundAreDisposed()
        {
            Outcome outcome = Load(Reply.TooManyRequests, Reply.TooManyRequests, Reply.NotFound);

            Assert.AreEqual(3, outcome.Responses.Count);
            AssertAllDisposed(outcome);
            Assert.IsTrue(outcome.TextureNotFound);
        }

        [TestMethod]
        public void ServiceUnavailableAndRequestTimeoutAreRetriedWithJitterAndDisposed()
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            Outcome outcome = Load(Reply.ServiceUnavailable, Reply.RequestTimeout, Reply.NotFound);
            elapsed.Stop();

            Assert.AreEqual(3, outcome.Responses.Count);
            AssertAllDisposed(outcome);
            Assert.IsTrue(outcome.TextureNotFound);
            Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(10),
                $"503 Retry-After must not be honored (only 429's is); took {elapsed.Elapsed.TotalSeconds:0.0} s.");
        }

        [TestMethod]
        public void TooManyRequestsWaitsForRetryAfterBeforeRetrying()
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            Outcome outcome = Load(Reply.TooManyRequestsWaitOneSecond, Reply.NotFound);
            elapsed.Stop();

            Assert.AreEqual(2, outcome.Responses.Count);
            AssertAllDisposed(outcome);
            Assert.IsTrue(elapsed.Elapsed >= TimeSpan.FromMilliseconds(900),
                $"Retry came after {elapsed.Elapsed.TotalMilliseconds:0} ms; Retry-After was 1 s.");
        }

        [TestMethod]
        public void ExhaustedRetriesStopAfterSixRequestsWithAllDisposed()
        {
            Outcome outcome = Load(Reply.TooManyRequests);

            Assert.AreEqual(6, outcome.Responses.Count);
            AssertAllDisposed(outcome);
            Assert.IsFalse(outcome.TextureNotFound);
        }

        /// <summary>
        /// Any run of retried 429s followed by any terminal reply: the reader stops at the first non-retry reply or
        /// after six requests, disposes every response, and marks TextureNotFound only when it stopped on a 404.
        /// </summary>
        [TestMethod]
        public void EveryResponseIsDisposedForAnyReplySequence()
        {
            Reply[] terminals = [Reply.OkNotAnImage, Reply.OkPng, Reply.NotFound, Reply.InternalServerError, Reply.Forbidden];
            Gen<Reply[]> scripts =
                from retries in Gen.Choose(0, 7)
                from terminal in Gen.Elements(terminals)
                select Enumerable.Repeat(Reply.TooManyRequests, retries).Append(terminal).ToArray();

            Prop.ForAll(Arb.From(scripts), (Reply[] script) =>
            {
                Outcome outcome = Load(script);

                int expectedRequests = Math.Min(script.Length, 6);
                Assert.AreEqual(expectedRequests, outcome.Responses.Count);
                AssertAllDisposed(outcome);
                bool stoppedOnNotFound = expectedRequests == script.Length && script[script.Length - 1] == Reply.NotFound;
                Assert.AreEqual(stoppedOnNotFound, outcome.TextureNotFound);
            }).QuickCheckThrowOnFailure();
        }
    }
}
