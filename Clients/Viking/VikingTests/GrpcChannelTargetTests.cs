using System.Linq;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Services.Grpc;
using Viking.UI.WPF.Services;

namespace VikingTests
{
    /// <summary>
    /// The segmentation server only speaks TLS, so every endpoint maps to a TLS target. These tests
    /// pin how a saved endpoint that was written for the old cleartext listener is carried over.
    /// <see cref="GrpcChannelTarget.TryFormat"/> serves both the shared segmentation channel and the
    /// Select Segmentation Service picker's status probe, so the probe checks the address segmentation dials.
    /// </summary>
    [TestClass]
    public class GrpcChannelTargetTests
    {
        [TestMethod]
        public void HttpsCustomPort_KeepsPortAndIsNotAnUpgrade()
        {
            GrpcChannelTarget? target = GrpcChannelTarget.TryFormat(
                "https://segmentation.codepharm.net:40443");

            Assert.IsNotNull(target);
            Assert.AreEqual("segmentation.codepharm.net:40443", target.Value.Target);
            Assert.IsFalse(target.Value.UpgradedFromPlaintext);
        }

        [TestMethod]
        public void HttpsWithoutPort_UsesPort443()
        {
            GrpcChannelTarget? target = GrpcChannelTarget.TryFormat(
                "https://segmentation.codepharm.net/");

            Assert.IsNotNull(target);
            Assert.AreEqual("segmentation.codepharm.net:443", target.Value.Target);
            Assert.IsFalse(target.Value.UpgradedFromPlaintext);
        }

        [TestMethod]
        public void HttpAndBareHostPort_AreUpgradedToTlsOnTheSamePort()
        {
            GrpcChannelTarget? http = GrpcChannelTarget.TryFormat(
                "http://segmentation.example:50051/");
            GrpcChannelTarget? bare = GrpcChannelTarget.TryFormat(
                "segmentation.example:50051");

            Assert.IsNotNull(http);
            Assert.IsNotNull(bare);
            Assert.AreEqual("segmentation.example:50051", http.Value.Target);
            Assert.AreEqual("segmentation.example:50051", bare.Value.Target);
            Assert.IsTrue(http.Value.UpgradedFromPlaintext);
            Assert.IsTrue(bare.Value.UpgradedFromPlaintext);
            Assert.AreEqual(http.Value.ChannelKey, GrpcChannelTarget.TryFormat(
                "https://segmentation.example:50051")!.Value.ChannelKey);
        }

        [TestMethod]
        public void TheOldCleartextPort_MovesToTheTlsPort()
        {
            GrpcChannelTarget? http = GrpcChannelTarget.TryFormat(
                "http://segmentation.codepharm.net:40080/");
            GrpcChannelTarget? bare = GrpcChannelTarget.TryFormat(
                "segmentation.codepharm.net:40080");

            Assert.IsNotNull(http);
            Assert.IsNotNull(bare);
            Assert.AreEqual("segmentation.codepharm.net:40443", http.Value.Target);
            Assert.AreEqual("segmentation.codepharm.net:40443", bare.Value.Target);
        }

        [TestMethod]
        public void AnHttpsEndpointOnThePortThatUsedToBeCleartext_IsLeftAlone()
        {
            GrpcChannelTarget? target = GrpcChannelTarget.TryFormat(
                "https://segmentation.example:40080");

            Assert.IsNotNull(target);
            Assert.AreEqual("segmentation.example:40080", target.Value.Target);
        }

        [TestMethod]
        public void ChannelOptions_PingOnlyDuringCallsAndReconnectQuietly()
        {
            var options = GrpcChannelManager.CreateChannelOptions().ToDictionary(o => o.Name, o => o.IntValue);

            // Faster than the server's 5 minute minimum ping interval and it drops the connection (GOAWAY too_many_pings).
            Assert.IsTrue(options["grpc.keepalive_time_ms"] > 5 * 60 * 1000);
            Assert.AreEqual(GrpcChannelManager.KeepaliveTimeMs, options["grpc.keepalive_time_ms"]);
            // Pinging with no call open would break the server's 2 hour idle ping limit and get the connection closed.
            Assert.AreEqual(0, options["grpc.keepalive_permit_without_calls"]);
            Assert.AreEqual(15_000, options["grpc.max_reconnect_backoff_ms"]);
            Assert.AreEqual(1, options["grpc.use_local_subchannel_pool"]);
            Assert.AreEqual(GrpcChannelManager.MaxMessageBytes, options[ChannelOptions.MaxReceiveMessageLength]);
            Assert.AreEqual(GrpcChannelManager.MaxMessageBytes, options[ChannelOptions.MaxSendMessageLength]);
        }

        [TestMethod]
        public void PortlessHttpAndBareHost_KeepPort80()
        {
            // The picker's probe used to drop a scheme-default port and let TLS default to 443, while the
            // segmentation channel dialed port 80: the picker could show a server live that segmentation could not reach.
            Assert.AreEqual("segmentation.example:80", GrpcChannelTarget.TryFormat("http://segmentation.example")!.Value.Target);
            Assert.AreEqual("segmentation.example:80", GrpcChannelTarget.TryFormat("  segmentation.example  ")!.Value.Target);
        }

        [TestMethod]
        public void PathAndQuery_AreKeptAfterThePort()
        {
            GrpcChannelTarget? target = GrpcChannelTarget.TryFormat("https://segmentation.example:40443/sam2?pool=a");

            Assert.AreEqual("segmentation.example:40443/sam2?pool=a", target!.Value.Target);
        }

        [TestMethod]
        public void Ipv6Literal_IsBracketedOnce()
        {
            Assert.AreEqual("[::1]:40443", GrpcChannelTarget.TryFormat("http://[::1]:40080/")!.Value.Target);
            Assert.AreEqual("[2001:db8::5]:443", GrpcChannelTarget.TryFormat("https://[2001:db8::5]")!.Value.Target);
        }

        [TestMethod]
        public void ChannelKey_NamesTheTlsTarget()
        {
            Assert.AreEqual("tls|segmentation.example:40443", GrpcChannelTarget.TryFormat("segmentation.example:40080")!.Value.ChannelKey);
        }

        [TestMethod]
        public void BlankOrUnsupportedEndpoints_HaveNoTarget()
        {
            Assert.IsNull(GrpcChannelTarget.TryFormat(null));
            Assert.IsNull(GrpcChannelTarget.TryFormat("  "));
            Assert.IsNull(GrpcChannelTarget.TryFormat("ftp://segmentation.example:21"));
            Assert.IsNull(GrpcChannelTarget.TryFormat("http://"));
        }
    }
}
