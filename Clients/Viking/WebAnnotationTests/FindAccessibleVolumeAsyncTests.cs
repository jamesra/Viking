using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Tokens;

namespace WebAnnotationTests
{
    /// <summary>
    /// <see cref="IdentityApiHelper.FindAccessibleVolumeAsync"/> against a local listener: only a 404 (legacy server)
    /// may fall back to the volume name; every other failure must surface, because continuing mints a token scoped
    /// by the wrong name that the annotation service then denies.
    /// </summary>
    [TestClass]
    public class FindAccessibleVolumeAsyncTests
    {
        private const string RowJson =
            @"{""7"":{""id"":7,""name"":""RC1-Pitt"",""annotationServerName"":""RC1"",""permissions"":[""Read""]}}";

        [TestMethod]
        public async Task NotFoundMeansLegacyServerAndReturnsNull()
        {
            using var server = OneShotServer.Start(HttpStatusCode.NotFound, "");

            var match = await FindAsync(server, "RC1-Pitt");

            Assert.IsNull(match);
        }

        [TestMethod]
        public async Task ServerErrorIsThrownNotSwallowed()
        {
            using var server = OneShotServer.Start(HttpStatusCode.InternalServerError, "");

            var ex = await Assert.ThrowsExceptionAsync<IdentityApiStatusException>(() => FindAsync(server, "RC1-Pitt"));

            Assert.AreEqual(HttpStatusCode.InternalServerError, ex.StatusCode);
        }

        [TestMethod]
        public async Task SuccessReturnsRowWithAnnotationServerName()
        {
            using var server = OneShotServer.Start(HttpStatusCode.OK, RowJson);

            var match = await FindAsync(server, "RC1-Pitt");

            Assert.AreEqual("RC1", match.AnnotationServerName);
        }

        private static Task<UserResourcePermissions> FindAsync(OneShotServer server, string name)
        {
            var helper = new IdentityApiHelper { IdentityApiURL = server.BaseUri };
            return helper.FindAccessibleVolumeAsync(TokenResponseFactory.FromAccessToken("hdr.pay.sig"), name);
        }

        /// <summary>Answers exactly one request with a fixed status and JSON body on a free loopback port.</summary>
        private sealed class OneShotServer : IDisposable
        {
            private readonly HttpListener _listener;

            public Uri BaseUri { get; }

            private OneShotServer(HttpListener listener, Uri baseUri)
            {
                _listener = listener;
                BaseUri = baseUri;
            }

            public static OneShotServer Start(HttpStatusCode status, string body)
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();

                var baseUri = new Uri($"http://localhost:{port}/");
                var listener = new HttpListener();
                listener.Prefixes.Add(baseUri.ToString());
                listener.Start();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var context = await listener.GetContextAsync();
                        byte[] bytes = Encoding.UTF8.GetBytes(body);
                        context.Response.StatusCode = (int)status;
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        context.Response.Close();
                    }
                    catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException)
                    {
                        // The test finished and disposed the listener before a request arrived.
                    }
                });

                return new OneShotServer(listener, baseUri);
            }

            public void Dispose() => _listener.Close();
        }
    }
}
