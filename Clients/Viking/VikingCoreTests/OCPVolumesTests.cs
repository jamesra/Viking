using System;
using System.Linq;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="OCPVolumes.ReadServer"/> against a loopback HTTP server: a JSON string array comes
    /// back as-is, a JSON null as an empty array, and every failure the volume list can hit (bad JSON,
    /// HTTP error, refused connection, non-http scheme) as a single entry holding an error message instead of an exception,
    /// because <c>VolumeListControl</c> shows the result directly and has no catch of its own.
    /// </summary>
    [TestClass]
    public class OCPVolumesTests
    {
        private static Uri Tokens(LoopbackHttpServer server) => new(server.BaseUri, "public_tokens");

        private static Gen<string> ValidUnicodeString() =>
            Arb.Default.NonNull<string>().Generator
                .Select(s => new string(s.Get.Where(c => !char.IsSurrogate(c)).ToArray()));

        [TestMethod]
        public void JsonStringArray_RoundTrips()
        {
            using LoopbackHttpServer server = new();
            Prop.ForAll(Arb.From(Gen.ArrayOf(ValidUnicodeString())), (string[] tokens) =>
            {
                server.Body = JsonConvert.SerializeObject(tokens);
                CollectionAssert.AreEqual(tokens, OCPVolumes.ReadServer(Tokens(server)));
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void KnownTokenList_RoundTrips()
        {
            using LoopbackHttpServer server = new() { Body = "[\"kasthuri11\",\"bock11\",\"ac4\"]" };
            CollectionAssert.AreEqual(new[] { "kasthuri11", "bock11", "ac4" }, OCPVolumes.ReadServer(Tokens(server)));
        }

        [TestMethod]
        public void JsonNull_ReturnsEmpty()
        {
            using LoopbackHttpServer server = new() { Body = "null" };
            Assert.AreEqual(0, OCPVolumes.ReadServer(Tokens(server)).Length);
        }

        [TestMethod]
        public void InvalidJson_ReturnsParserMessage()
        {
            const string body = "<html>not json</html>";
            string expected = "";
            try
            {
                JsonConvert.DeserializeObject<string[]>(body);
                Assert.Fail("Body was expected to be invalid JSON.");
            }
            catch (JsonReaderException e)
            {
                expected = e.Message;
            }

            using LoopbackHttpServer server = new() { Body = body, ContentType = "text/html; charset=utf-8" };
            CollectionAssert.AreEqual(new[] { expected }, OCPVolumes.ReadServer(Tokens(server)));
        }

        [TestMethod]
        public void HttpError_ReturnsOneMessageNamingTheStatus()
        {
            using LoopbackHttpServer server = new() { Status = "404 Not Found", Body = "missing" };
            string[] result = OCPVolumes.ReadServer(Tokens(server));
            Assert.AreEqual(1, result.Length);
            StringAssert.Contains(result[0], "404");
        }

        [TestMethod]
        public void RefusedConnection_ReturnsConnectFailureMessage()
        {
            Uri uri = new($"http://127.0.0.1:{LoopbackHttpServer.UnusedPort()}/public_tokens");
            CollectionAssert.AreEqual(new[] { "Unable to connect to the remote server" }, OCPVolumes.ReadServer(uri));
        }

        /// <summary>
        /// Waits out the client's built-in 100 second request timeout; there is no seam to shorten it.
        /// </summary>
        [TestMethod]
        [TestCategory("Slow")]
        [Timeout(180_000)]
        public void ServerNeverAnswers_ReturnsOneMessageAfterTimeout()
        {
            using LoopbackHttpServer server = new() { NeverRespond = true };
            string[] result = OCPVolumes.ReadServer(Tokens(server));
            Assert.AreEqual(1, result.Length);
            Assert.IsFalse(string.IsNullOrEmpty(result[0]));
        }

        [TestMethod]
        public void NonHttpScheme_ReturnsOneMessage()
        {
            Uri uri = new($"ftp://127.0.0.1:{LoopbackHttpServer.UnusedPort()}/public_tokens");
            string[] result = OCPVolumes.ReadServer(uri);
            Assert.AreEqual(1, result.Length);
            Assert.IsFalse(string.IsNullOrEmpty(result[0]));
        }
    }
}
