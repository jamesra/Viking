using System;
using System.Net;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins <see cref="HttpClientFactory"/> scheme rules: HTTPS handlers take the supplied
    /// <see cref="ICredentials"/>; HTTP handlers enable <see cref="HttpClientHandler.UseDefaultCredentials"/>.
    /// Callers pass credentials in; this factory only wires handler flags (no token storage).
    /// </summary>
    [TestClass]
    public class HttpClientFactoryTests
    {
        private sealed class StubCredentials : ICredentials
        {
            public NetworkCredential GetCredential(Uri uri, string authType) =>
                new NetworkCredential("loop-user", "loop-pass");
        }

        [TestMethod]
        public void CreateHandler_Https_UsesPassedCredentials_NotDefaultCredentials()
        {
            var creds = new StubCredentials();
            using var handler = HttpClientFactory.CreateHandler(new Uri("https://host.example/path"), creds);
            Assert.AreSame(creds, handler.Credentials);
            Assert.IsFalse(handler.UseDefaultCredentials);
        }

        [TestMethod]
        public void CreateHandler_HttpsSchemeCaseInsensitive_StillUsesCredentials()
        {
            var creds = new StubCredentials();
            using var handler = HttpClientFactory.CreateHandler(new Uri("HTTPS://HOST.EXAMPLE/"), creds);
            Assert.AreSame(creds, handler.Credentials);
            Assert.IsFalse(handler.UseDefaultCredentials);
        }

        [TestMethod]
        public void CreateHandler_Https_NullCredentials_AllowsNull()
        {
            using var handler = HttpClientFactory.CreateHandler(new Uri("https://host/"), null);
            Assert.IsNull(handler.Credentials);
            Assert.IsFalse(handler.UseDefaultCredentials);
        }

        [TestMethod]
        public void CreateHandler_Http_UsesDefaultCredentials_IgnoresPassedCredentials()
        {
            var creds = new StubCredentials();
            using var handler = HttpClientFactory.CreateHandler(new Uri("http://host.example/path"), creds);
            Assert.IsTrue(handler.UseDefaultCredentials);
            Assert.IsNull(handler.Credentials);
        }

        [TestMethod]
        public void CreateHandler_HttpSchemeCaseInsensitive_UsesDefaultCredentials()
        {
            using var handler = HttpClientFactory.CreateHandler(new Uri("HTTP://HOST/"), new StubCredentials());
            Assert.IsTrue(handler.UseDefaultCredentials);
        }

        [TestMethod]
        public void CreateHandler_NonHttpSchemes_UseDefaultCredentialsLikeHttp()
        {
            using var handler = HttpClientFactory.CreateHandler(new Uri("ftp://files.example/resource"), new StubCredentials());
            Assert.IsTrue(handler.UseDefaultCredentials);
            Assert.IsNull(handler.Credentials);
        }

        [TestMethod]
        public void CreateClient_Https_DisposesWithoutNetwork()
        {
            var creds = new StubCredentials();
            using var client = HttpClientFactory.CreateClient(new Uri("https://host.example/"), creds);
            Assert.IsNotNull(client);
        }

        [TestMethod]
        public void CreateHandler_Scheme_PropertyMatchesHttpsVsHttpRules()
        {
            var schemes = Arb.From(Gen.Elements("http", "HTTP", "https", "HTTPS"));
            Prop.ForAll(schemes, scheme =>
            {
                var uri = new Uri($"{scheme}://example.test/resource");
                var creds = new StubCredentials();
                using var handler = HttpClientFactory.CreateHandler(uri, creds);
                bool isHttps = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
                if (isHttps)
                {
                    return ReferenceEquals(handler.Credentials, creds) && !handler.UseDefaultCredentials;
                }

                return handler.UseDefaultCredentials && handler.Credentials == null;
            }).QuickCheckThrowOnFailure();
        }
    }
}
