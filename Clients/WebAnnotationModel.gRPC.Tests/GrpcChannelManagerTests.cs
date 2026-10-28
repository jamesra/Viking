using System;
using System.Net.Http;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins <see cref="GrpcChannelManager"/> endpoint rewriting and channel reuse. Runs without a server:
    /// <see cref="GrpcChannel.ForAddress(Uri, GrpcChannelOptions)"/> does not connect until the first call.
    /// The bearer-token handler is deliberately not exercised.
    /// </summary>
    [TestFixture]
    public class GrpcChannelManagerTests
    {
        /// <summary>
        /// Stands in for System.Net.Http.WinHttpHandler, which the manager recognizes by type name only.
        /// </summary>
        private sealed class WinHttpHandler : HttpMessageHandler
        {
            protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
                => throw new InvalidOperationException("no server in this test");
        }

        private sealed class Wrapper : DelegatingHandler
        {
            public Wrapper(HttpMessageHandler inner) : base(inner) { }
        }

        private static Uri Normalize(string endpoint, HttpMessageHandler handler)
            => GrpcChannelManager.NormalizeEndpointForHandler(new Uri(endpoint), handler);

        [Test]
        public void NullEndpointIsReturnedUnchanged()
        {
            Assert.That(GrpcChannelManager.NormalizeEndpointForHandler(null, new WinHttpHandler()), Is.Null);
        }

        [Test]
        public void NonWinHttpHandlerLeavesHttpEndpointUntouched()
        {
            var uri = new Uri("http://localhost:5010/");
            Assert.That(GrpcChannelManager.NormalizeEndpointForHandler(uri, null), Is.SameAs(uri));
            Assert.That(GrpcChannelManager.NormalizeEndpointForHandler(uri, new HttpClientHandler()), Is.SameAs(uri));
            Assert.That(GrpcChannelManager.NormalizeEndpointForHandler(uri, new Wrapper(new HttpClientHandler())), Is.SameAs(uri));
        }

        [Test]
        public void HttpsEndpointIsUnchangedForWinHttpHandler()
        {
            var uri = new Uri("https://localhost:5010/");
            Assert.That(GrpcChannelManager.NormalizeEndpointForHandler(uri, new WinHttpHandler()), Is.SameAs(uri));
        }

        [TestCase("localhost")]
        [TestCase("LOCALHOST")]
        [TestCase("127.0.0.1")]
        [TestCase("[::1]")]
        public void LoopbackPort5010MapsToHttps5011(string host)
        {
            var result = Normalize($"http://{host}:5010/", new WinHttpHandler());
            Assert.That(result.Scheme, Is.EqualTo("https"));
            Assert.That(result.Port, Is.EqualTo(5011));
        }

        [Test]
        public void WinHttpHandlerWrappedInDelegatingHandlersIsRecognized()
        {
            var result = Normalize("http://localhost:5010/", new Wrapper(new Wrapper(new WinHttpHandler())));
            Assert.That(result, Is.EqualTo(new Uri("https://localhost:5011/")));
        }

        [Test]
        public void OtherLoopbackPortKeepsItsPort()
        {
            var result = Normalize("http://localhost:6000/", new WinHttpHandler());
            Assert.That(result, Is.EqualTo(new Uri("https://localhost:6000/")));
        }

        [Test]
        public void NonLoopbackHostKeepsPort5010()
        {
            var result = Normalize("http://server.example.org:5010/path", new WinHttpHandler());
            Assert.That(result, Is.EqualTo(new Uri("https://server.example.org:5010/path")));
        }

        private static GrpcChannelManager NewManager()
            => new GrpcChannelManager(Options.Create(new GrpcChannelOptions()));

        [Test]
        public void GetOrCreateReusesChannelForSameEndpoint()
        {
            var manager = NewManager();
            var first = manager.GetOrCreate(new Uri("http://localhost:5010/"));
            var second = manager.GetOrCreate(new Uri("http://localhost:5010/"));
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void GetOrCreateReturnsDistinctChannelsForDifferentEndpoints()
        {
            var manager = NewManager();
            var a = manager.GetOrCreate(new Uri("http://localhost:5010/"));
            var b = manager.GetOrCreate(new Uri("http://localhost:5020/"));
            Assert.That(b, Is.Not.SameAs(a));
        }

        [Test]
        public void GetOrCreateSharesChannelWhenRewrittenEndpointsCollide()
        {
            var options = new GrpcChannelOptions { HttpHandler = new WinHttpHandler() };
            var manager = new GrpcChannelManager(Options.Create(options));
            var http = manager.GetOrCreate(new Uri("http://localhost:5010/"));
            var https = manager.GetOrCreate(new Uri("https://localhost:5011/"));
            Assert.That(https, Is.SameAs(http));
        }
    }
}
