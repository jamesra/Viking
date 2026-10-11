using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Viking.GrpcSectionCorrectionService;

namespace GrpcSectionCorrectionService.Tests
{
    [TestClass]
    public class IdentityAssignmentClientTests
    {
        static readonly IdentityAssignmentOptions Options = new()
        {
            Endpoint = "https://identity.example:5001/",
            ApiEndpoint = "https://identity.example:6001/",
            ClientId = "correction",
            ClientSecret = "s3cret",
            ServiceName = "corr-a"
        };

        [TestMethod]
        public async Task ListAsync_GetsTokenThenAssignments_MapsRows()
        {
            var handler = new ScriptedHandler(request =>
            {
                if (request.RequestUri!.AbsolutePath == "/connect/token")
                    return Json("{\"access_token\":\"tok\",\"token_type\":\"Bearer\"}");
                Assert.AreEqual("Bearer tok", request.Headers.Authorization!.ToString());
                Assert.AreEqual("https://identity.example:6001/Permissions/AssignedAnnotationContexts?service=corr-a",
                    request.RequestUri.ToString());
                return Json("[{\"annotationContextId\":4,\"name\":\"RC1\",\"endpoint\":\"http://x/RC1.vikingxml\"," +
                            "\"registrationName\":\"grid\",\"annotationDatabaseName\":\"RC1db\"," +
                            "\"annotationEndpoint\":\"https://ann/RC1/Annotation/Service.svc\",\"oDataEndpoint\":\"https://ann/RC1/OData/\"}]");
            });
            var client = new IdentityAssignmentClient(new HttpClient(handler), Options, NullLogger<IdentityAssignmentClient>.Instance);

            IReadOnlyList<IdentityVolumeRow> rows = await client.ListAsync(CancellationToken.None);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(new IdentityVolumeRow("RC1", "http://x/RC1.vikingxml", "https://ann/RC1/Annotation/Service.svc",
                "RC1db", "grid", "https://ann/RC1/OData/"), rows[0]);
            Assert.AreEqual("RC1db", rows[0].CorrectionDatabase);
        }

        [TestMethod]
        public async Task ListAsync_RetriesWhileIdentityIsDown()
        {
            int tokenCalls = 0;
            var handler = new ScriptedHandler(request =>
            {
                if (request.RequestUri!.AbsolutePath == "/connect/token")
                {
                    if (++tokenCalls < 3)
                        throw new HttpRequestException("connection refused");
                    return Json("{\"access_token\":\"tok\"}");
                }

                return Json("[]");
            });
            var client = new IdentityAssignmentClient(
                new HttpClient(handler), Options, NullLogger<IdentityAssignmentClient>.Instance,
                maxAttempts: 5, firstDelay: TimeSpan.FromMilliseconds(1));

            IReadOnlyList<IdentityVolumeRow> rows = await client.ListAsync(CancellationToken.None);

            Assert.AreEqual(0, rows.Count);
            Assert.AreEqual(3, tokenCalls);
        }

        [TestMethod]
        public async Task ListAsync_UnknownService_DoesNotRetry()
        {
            int apiCalls = 0;
            var handler = new ScriptedHandler(request =>
            {
                if (request.RequestUri!.AbsolutePath == "/connect/token")
                    return Json("{\"access_token\":\"tok\"}");
                apiCalls++;
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });
            var client = new IdentityAssignmentClient(
                new HttpClient(handler), Options, NullLogger<IdentityAssignmentClient>.Instance,
                maxAttempts: 5, firstDelay: TimeSpan.FromMilliseconds(1));

            await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.ListAsync(CancellationToken.None));
            Assert.AreEqual(1, apiCalls);
        }

        static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(respond(request));
        }
    }
}
