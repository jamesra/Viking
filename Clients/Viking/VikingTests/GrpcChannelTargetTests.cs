using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Services.Grpc;

namespace VikingTests
{
    [TestClass]
    public class GrpcChannelTargetTests
    {
        [TestMethod]
        public void HttpsCustomPort_UsesTlsAndKeepsPort()
        {
            GrpcChannelTarget? target = GrpcChannelManager.TryFormatChannelTarget(
                "https://segmentation.codepharm.net:40443");

            Assert.IsNotNull(target);
            Assert.AreEqual("segmentation.codepharm.net:40443", target.Value.Target);
            Assert.IsTrue(target.Value.UseTransportSecurity);
        }

        [TestMethod]
        public void HttpsWithoutPort_UsesTlsOn443()
        {
            GrpcChannelTarget? target = GrpcChannelManager.TryFormatChannelTarget(
                "https://segmentation.codepharm.net/");

            Assert.IsNotNull(target);
            Assert.AreEqual("segmentation.codepharm.net:443", target.Value.Target);
            Assert.IsTrue(target.Value.UseTransportSecurity);
        }

        [TestMethod]
        public void HttpAndBareHostPort_StayPlaintext()
        {
            GrpcChannelTarget? http = GrpcChannelManager.TryFormatChannelTarget(
                "http://segmentation.example:50051/");
            GrpcChannelTarget? bare = GrpcChannelManager.TryFormatChannelTarget(
                "segmentation.example:50051");

            Assert.IsNotNull(http);
            Assert.IsNotNull(bare);
            Assert.AreEqual("segmentation.example:50051", http.Value.Target);
            Assert.AreEqual("segmentation.example:50051", bare.Value.Target);
            Assert.IsFalse(http.Value.UseTransportSecurity);
            Assert.IsFalse(bare.Value.UseTransportSecurity);
            Assert.AreNotEqual(http.Value.ChannelKey, GrpcChannelManager.TryFormatChannelTarget(
                "https://segmentation.example:50051")!.Value.ChannelKey);
        }
    }
}
