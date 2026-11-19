using Grpc.Net.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Net.Http;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPC_Tests
{
    [TestClass]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class LocationTests
    {
        // The DevTest stack's own port (Start-AnnotationTestStack.ps1). Production volumes use 5011 and up; never aim here.
        private const string TestStackEndpoint = "https://localhost:5019";

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

        [TestMethod]
        public void Test_GetLocationByID()
        {
            EnsureDevTestStack();
            using var channel = GrpcChannel.ForAddress(TestStackEndpoint);
            long Id = 4;           
            AnnotateLocations.AnnotateLocationsClient client = new AnnotateLocations.AnnotateLocationsClient(channel);

            var reply = client.GetLocationByID(new GetLocationByIDRequest() { Id = Id });

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
