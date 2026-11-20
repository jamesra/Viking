using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="ConditionalGet"/>: the date sent as <c>If-Modified-Since</c>, how each status maps to an outcome,
    /// and stamping a cache file with the server's <c>Last-Modified</c>.
    /// </summary>
    [TestClass]
    public class ConditionalGetTests
    {
        private string _folder = "";

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ConditionalGet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        /// <summary>Answers every request with <see cref="Status"/> and records the requests' <c>If-Modified-Since</c>.</summary>
        internal sealed class StatusHandler : HttpMessageHandler
        {
            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
            public DateTimeOffset? LastModified { get; set; }
            public List<DateTimeOffset?> IfModifiedSince { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (IfModifiedSince)
                    IfModifiedSince.Add(request.Headers.IfModifiedSince);
                var response = new HttpResponseMessage(Status) { Content = new ByteArrayContent([1, 2, 3]), RequestMessage = request };
                response.Content.Headers.LastModified = LastModified;
                return Task.FromResult(response);
            }
        }

        private string NewFile(byte[] content, DateTime utcWrite)
        {
            string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, content);
            File.SetLastWriteTimeUtc(path, utcWrite);
            return path;
        }

        private static readonly Uri Resource = new("http://server.test/tile.png");

        [TestMethod]
        public void ValidatorIsTheCacheFilesWriteTime()
        {
            DateTime written = new(2026, 11, 5, 19, 6, 38, DateTimeKind.Utc);
            string file = NewFile([1], written);

            Assert.AreEqual(new DateTimeOffset(written), ConditionalGet.ValidatorFor(file));
        }

        [TestMethod]
        public void MissingOrEmptyCacheFileHasNoValidator()
        {
            Assert.IsNull(ConditionalGet.ValidatorFor(Path.Combine(_folder, "absent.png")));
            Assert.IsNull(ConditionalGet.ValidatorFor(NewFile([], DateTime.UtcNow)));
            Assert.IsNull(ConditionalGet.ValidatorFor((string?)null));
        }

        [TestMethod]
        public void StoredDateValidatorTreatsMinAndMaxAsUnknownAndUnspecifiedAsUtc()
        {
            Assert.IsNull(ConditionalGet.ValidatorFor(DateTime.MinValue));
            Assert.IsNull(ConditionalGet.ValidatorFor(DateTime.MaxValue));
            DateTime unspecified = new(2026, 9, 9, 1, 36, 11, DateTimeKind.Unspecified);
            Assert.AreEqual(new DateTimeOffset(2026, 9, 9, 1, 36, 11, TimeSpan.Zero), ConditionalGet.ValidatorFor(unspecified));
        }

        [TestMethod]
        public void SendsTheValidatorAsIfModifiedSince()
        {
            var handler = new StatusHandler { Status = HttpStatusCode.NotModified };
            DateTimeOffset since = new(2026, 11, 5, 19, 6, 38, TimeSpan.Zero);

            using (ConditionalGetResult result = ConditionalGet.SendAsync(new HttpClient(handler), Resource, since, CancellationToken.None).Result)
                Assert.AreEqual(ConditionalGetOutcome.NotModified, result.Outcome);

            CollectionAssert.AreEqual(new DateTimeOffset?[] { since }, handler.IfModifiedSince);
        }

        [TestMethod]
        public void NoValidatorSendsAnUnconditionalGet()
        {
            var handler = new StatusHandler();

            using (ConditionalGet.SendAsync(new HttpClient(handler), Resource, (DateTimeOffset?)null, CancellationToken.None).Result)
            {
            }

            CollectionAssert.AreEqual(new DateTimeOffset?[] { null }, handler.IfModifiedSince);
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.OK, ConditionalGetOutcome.Modified)]
        [DataRow(HttpStatusCode.NotModified, ConditionalGetOutcome.NotModified)]
        [DataRow(HttpStatusCode.NotFound, ConditionalGetOutcome.NotFound)]
        [DataRow(HttpStatusCode.Gone, ConditionalGetOutcome.NotFound)]
        [DataRow(HttpStatusCode.InternalServerError, ConditionalGetOutcome.Failed)]
        [DataRow(HttpStatusCode.ServiceUnavailable, ConditionalGetOutcome.Failed)]
        public void StatusMapsToOutcome(HttpStatusCode status, ConditionalGetOutcome expected)
        {
            var handler = new StatusHandler { Status = status, LastModified = DateTimeOffset.UtcNow };

            using ConditionalGetResult result = ConditionalGet.SendAsync(new HttpClient(handler), Resource, DateTimeOffset.UtcNow, CancellationToken.None).Result;

            Assert.AreEqual(expected, result.Outcome);
            Assert.AreEqual(status, result.StatusCode);
            Assert.AreEqual(expected == ConditionalGetOutcome.Modified, result.Response is not null, "Only a 2xx keeps its response.");
        }

        [TestMethod]
        public void UnreachableServerIsFailedNotAnException()
        {
            var uri = new Uri($"http://127.0.0.1:{LoopbackHttpServer.UnusedPort()}/tile.png");

            using ConditionalGetResult result = ConditionalGet.SendAsync(new HttpClient(), uri, DateTimeOffset.UtcNow, CancellationToken.None).Result;

            Assert.AreEqual(ConditionalGetOutcome.Failed, result.Outcome);
            Assert.IsNull(result.StatusCode);
        }

        [TestMethod]
        public void CallerCancellationThrows()
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            var e = Assert.ThrowsException<AggregateException>(() =>
                ConditionalGet.SendAsync(new HttpClient(new StatusHandler()), Resource, DateTimeOffset.UtcNow, cancelled.Token).Wait());
            Assert.IsInstanceOfType(e.InnerException, typeof(OperationCanceledException));
        }

        [TestMethod]
        public void HasValidatorFollowsLastModified()
        {
            using (ConditionalGetResult with = ConditionalGet.SendAsync(
                       new HttpClient(new StatusHandler { LastModified = DateTimeOffset.UtcNow }), Resource, (DateTimeOffset?)null, CancellationToken.None).Result)
                Assert.IsTrue(with.HasValidator);

            using (ConditionalGetResult without = ConditionalGet.SendAsync(
                       new HttpClient(new StatusHandler()), Resource, (DateTimeOffset?)null, CancellationToken.None).Result)
                Assert.IsFalse(without.HasValidator);
        }

        [TestMethod]
        public void StampSetsTheWriteTimeToTheServersDate()
        {
            string file = NewFile([1], DateTime.UtcNow);
            DateTimeOffset serverDate = new(2026, 11, 5, 19, 6, 38, TimeSpan.Zero);

            Assert.IsTrue(ConditionalGet.StampCacheFile(file, serverDate));

            Assert.AreEqual(serverDate.UtcDateTime, File.GetLastWriteTimeUtc(file));
            Assert.AreEqual(serverDate, ConditionalGet.ValidatorFor(file), "The next request sends exactly the server's date.");
        }

        [TestMethod]
        public void StampWithoutADateOrFileDoesNothing()
        {
            DateTime written = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            string file = NewFile([1], written);

            Assert.IsFalse(ConditionalGet.StampCacheFile(file, null));
            Assert.IsFalse(ConditionalGet.StampCacheFile(Path.Combine(_folder, "absent.png"), DateTimeOffset.UtcNow));
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(file));
        }
    }
}
