using Grpc.Net.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPC_Tests
{
    [TestClass]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class LocationTests
    {
        [TestMethod]
        public void Test_GetLocationByID()
        {
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
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
