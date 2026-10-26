using System.Linq;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Services.Grpc;

namespace VikingTests
{
    /// <summary>
    /// The segmentation server only speaks TLS, so every endpoint maps to a TLS target. These tests
    /// pin how a saved endpoint that was written for the old cleartext listener is carried over.
    /// </summary>
    [TestClass]
    public class GrpcChannelTargetTests
    {
        [TestMethod]
        public void HttpsCustomPort_KeepsPortAndIsNotAnUpgrade()
        {
            GrpcChannelTarget? target = GrpcChannelManager.TryFormatChannelTarget(
                "https://segmentation.codepharm.net:40443");

            Assert.IsNotNull(target);
            Assert.AreEqual("segmentation.codepharm.net:40443", target.Value.Target);
            Assert.IsFalse(target.Value.UpgradedFromPlaintext);
        }

        [TestMethod]
        public void HttpsWithoutPort_UsesPort443()
        {
            GrpcChannelTarget? target = GrpcChannelManager.TryFormatChannelTarget(
                "https://segmentation.codepharm.net/");

            Assert.IsNotNull(target);
            Assert.AreEqual("segmentation.codepharm.net:443", target.Value.Target);
            Assert.IsFalse(target.Value.UpgradedFromPlaintext);
        }

        [TestMethod]
        public void HttpAndBareHostPort_AreUpgradedToTlsOnTheSamePort()
        {
            GrpcChannelTarget? http = GrpcChannelManager.TryFormatChannelTarget(
                "http://segmentation.example:50051/");
            GrpcChannelTarget? bare = GrpcChannelManager.TryFormatChannelTarget(
                "segmentation.example:50051");

            Assert.IsNotNull(http);
            Assert.IsNotNull(bare);
            Assert.AreEqual("segmentation.example:50051", http.Value.Target);
            Assert.AreEqual("segmentation.example:50051", bare.Value.Target);
            Assert.IsTrue(http.Value.UpgradedFromPlaintext);
            Assert.IsTrue(bare.Value.UpgradedFromPlaintext);
            Assert.AreEqual(http.Value.ChannelKey, GrpcChannelManager.TryFormatChannelTarget(
                "https://segmentation.example:50051")!.Value.ChannelKey);
        }

        [TestMethod]
        public void TheOldCleartextPort_MovesToTheTlsPort()
        {
            GrpcChannelTarget? http = GrpcChannelManager.TryFormatChannelTarget(
                "http://segmentation.codepharm.net:40080/");
            GrpcChannelTarget? bare = GrpcChannelManager.TryFormatChannelTarget(
                "segmentation.codepharm.net:40080");

            Assert.IsNotNull(http);
            Assert.IsNotNull(bare);
            Assert.AreEqual("segmentation.codepharm.net:40443", http.Value.Target);
            Assert.AreEqual("segmentation.codepharm.net:40443", bare.Value.Target);
        }

        [TestMethod]
        public void AnHttpsEndpointOnThePortThatUsedToBeCleartext_IsLeftAlone()
        {
            GrpcChannelTarget? target = GrpcChannelManager.TryFormatChannelTarget(
                "https://segmentation.example:40080");

            Assert.IsNotNull(target);
            Assert.AreEqual("segmentation.example:40080", target.Value.Target);
        }

        [TestMethod]
        public void ChannelOptions_KeepTheConnectionAliveAndReconnectQuietly()
        {
            var options = GrpcChannelManager.CreateChannelOptions().ToDictionary(o => o.Name, o => o.IntValue);

            // Faster than the server's 5 minute minimum ping interval and it drops the connection (GOAWAY too_many_pings).
            Assert.IsTrue(options["grpc.keepalive_time_ms"] > 5 * 60 * 1000);
            Assert.AreEqual(GrpcChannelManager.KeepaliveTimeMs, options["grpc.keepalive_time_ms"]);
            Assert.AreEqual(1, options["grpc.keepalive_permit_without_calls"]);
            Assert.AreEqual(15_000, options["grpc.max_reconnect_backoff_ms"]);
            Assert.AreEqual(1, options["grpc.use_local_subchannel_pool"]);
            Assert.AreEqual(GrpcChannelManager.MaxMessageBytes, options[ChannelOptions.MaxReceiveMessageLength]);
            Assert.AreEqual(GrpcChannelManager.MaxMessageBytes, options[ChannelOptions.MaxSendMessageLength]);
        }

        [TestMethod]
        public void BlankOrUnsupportedEndpoints_HaveNoTarget()
        {
            Assert.IsNull(GrpcChannelManager.TryFormatChannelTarget("  "));
            Assert.IsNull(GrpcChannelManager.TryFormatChannelTarget("ftp://segmentation.example:21"));
        }
    }
}
