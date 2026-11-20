using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPC_Tests
{
    [TestClass]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class LocationTests
    {
        // The DevTest stack's own port (Start-AnnotationTestStack.ps1). Production volumes use 5011 and up; never aim here.
        private const string TestStackEndpoint = "https://localhost:5019";

        // The DevTest Identity server the test stack trusts (Start-AnnotationTestStack.ps1).
        private const string TestIdentityEndpoint = "https://host.docker.internal:5020";

        /// <summary>
        /// Fails unless the server answers GET /test-stack, which only the DevTest stack maps
        /// (WebAnnotationModel.gRPC.Tests TestStackGuard holds the full check).
        /// </summary>
        private static void EnsureDevTestStack()
        {
            try
            {
                using var http = new HttpClient(new SocketsHttpHandler
                {
                    // Only this one request skips validation: it reads the marker and sends nothing else.
                    SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true }
                }) { Timeout = TimeSpan.FromSeconds(15) };
                string marker = http.GetStringAsync(TestStackEndpoint + "/test-stack").GetAwaiter().GetResult();
                if (!marker.Contains("\"testStack\":true"))
                    Assert.Fail($"Refusing to run against {TestStackEndpoint}: its /test-stack answer is not the DevTest marker.");
            }
            catch (HttpRequestException e)
            {
                Assert.Fail($"Refusing to run against {TestStackEndpoint}: GET /test-stack failed ({e.Message}). Start the DevTest stack first.");
            }
        }

        /// <summary>
        /// Trusts only a test stack's self-signed certificate, which Start-AnnotationTestStack.ps1 saves to %TEMP%:
        /// viking-annotation-test-stack.pem (gRPC) or viking-identity-test-stack.pem (DevTest Identity).
        /// WebAnnotationModel.gRPC.Tests TestServerTls does the same.
        /// </summary>
        private static SocketsHttpHandler PinnedTestStackHandler(string pemName = "viking-annotation-test-stack.pem")
        {
            string path = Path.Combine(Path.GetTempPath(), pemName);
            if (!File.Exists(path))
                Assert.Fail($"{path} is missing. Start the DevTest stack with Start-AnnotationTestStack.ps1, which saves it.");
            using var pinned = new X509Certificate2(path);
            string pinnedHash = pinned.GetCertHashString();
            return new SocketsHttpHandler
            {
                SslOptions =
                {
                    RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                        errors == SslPolicyErrors.None ||
                        string.Equals(certificate?.GetCertHashString(), pinnedHash, StringComparison.OrdinalIgnoreCase)
                }
            };
        }

        /// <summary>
        /// Signs in to the DevTest Identity server as its test user (Servers/IdentityServer/DevTest/Config.cs, the same
        /// user and client as WebAnnotationModel.gRPC.Tests appsettings.json) and returns the access token.
        /// </summary>
        private static string GetTestUserToken()
        {
            using var http = new HttpClient(PinnedTestStackHandler("viking-identity-test-stack.pem")) { Timeout = TimeSpan.FromSeconds(15) };
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "ro.viking",
                ["client_secret"] = "CorrectHorseBatteryStaple",
                ["username"] = "testuser",
                ["password"] = "Testing123!",
                ["scope"] = "openid Viking.Annotation",
            });
            using HttpResponseMessage response = http.PostAsync(TestIdentityEndpoint + "/connect/token", form).GetAwaiter().GetResult();
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                Assert.Fail($"DevTest Identity refused the test user ({(int)response.StatusCode}): {body}");
            using JsonDocument token = JsonDocument.Parse(body);
            return token.RootElement.GetProperty("access_token").GetString();
        }

        [TestMethod]
        public void Test_GetLocationByID()
        {
            EnsureDevTestStack();
            using var channel = GrpcChannel.ForAddress(TestStackEndpoint, new GrpcChannelOptions { HttpHandler = PinnedTestStackHandler() });
            // Location 1 is the seed row Reset-AnnotationTestDatabase.ps1 inserts.
            long Id = 1;
            AnnotateLocations.AnnotateLocationsClient client = new AnnotateLocations.AnnotateLocationsClient(channel);

            var auth = new Metadata { { "Authorization", "Bearer " + GetTestUserToken() } };
            var reply = client.GetLocationByID(new GetLocationByIDRequest() { Id = Id }, auth);

            Assert.IsNotNull(reply);
            Assert.IsNotNull(reply.Result);
            Assert.AreEqual(reply.Result.Id, Id);
        }

        private string GetDebuggerDisplay()
        {
            return ToString();
        }
    }
}
