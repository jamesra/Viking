using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins the URL rules of <see cref="IdentityEndpoints"/> that the cases in VikingTests leave open:
    /// blank or relative Identity <c>AnnotationEndpoint</c> values fall back to VikingXML, values are trimmed,
    /// the WebApi rewrite strips path/query/fragment and compares hosts ignoring case, and the scope resource
    /// name ignores blank server names. Login and the annotation module pick their WCF endpoint and OAuth scope
    /// with these helpers, so a regression points a session at the wrong server or the wrong scope.
    /// </summary>
    [TestClass]
    public class IdentityEndpointsTests
    {
        static readonly Uri IdentityServer = new Uri("https://identity.codepharm.net:5001/");
        static readonly Uri VolumeXmlEndpoint = new Uri("https://example.test/old/Annotation/Service.svc");

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t\r\n")]
        public void TryResolveAnnotationEndpointUri_NullOrBlank_ReturnsFalseAndNull(string value)
        {
            Assert.IsFalse(IdentityEndpoints.TryResolveAnnotationEndpointUri(value, out Uri endpoint));
            Assert.IsNull(endpoint);
        }

        [DataTestMethod]
        [DataRow("/RC2/Annotation/Service.svc")]
        [DataRow("RC2/Annotation/Service.svc")]
        [DataRow("not a url")]
        public void TryResolveAnnotationEndpointUri_RelativeOrMalformed_ReturnsFalse(string value)
        {
            Assert.IsFalse(IdentityEndpoints.TryResolveAnnotationEndpointUri(value, out _));
        }

        [TestMethod]
        public void TryResolveAnnotationEndpointUri_SurroundingWhitespace_IsTrimmed()
        {
            Assert.IsTrue(IdentityEndpoints.TryResolveAnnotationEndpointUri(
                "  https://example.test/RC2/Annotation/Service.svc \r\n", out Uri endpoint));
            Assert.AreEqual(new Uri("https://example.test/RC2/Annotation/Service.svc"), endpoint);
        }

        [TestMethod]
        public void TryResolveAnnotationEndpointUri_Property_AbsoluteHttpUrlRoundTripsWithPadding()
        {
            var hosts = Gen.Elements("example.test", "websvc.codepharm.net", "localhost", "10.0.0.5");
            var paths = Gen.Elements("", "RC2/Annotation/Service.svc", "RC1/Annotation/Service.svc", "a/b");
            var pads = Gen.Elements("", " ", "  ", "\t", "\r\n");
            var gen = from scheme in Gen.Elements("http", "https")
                      from host in hosts
                      from port in Gen.Choose(1, 65535)
                      from path in paths
                      from left in pads
                      from right in pads
                      select (url: $"{scheme}://{host}:{port}/{path}", left, right);

            Prop.ForAll(gen.ToArbitrary(), c =>
                IdentityEndpoints.TryResolveAnnotationEndpointUri(c.left + c.url + c.right, out Uri endpoint)
                && endpoint == new Uri(c.url))
                .QuickCheckThrowOnFailure();
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("  ")]
        [DataRow("/relative/Service.svc")]
        public void ResolveAnnotationServiceEndpoint_UnusableIdentityValue_FallsBackToXml(string identity)
        {
            Assert.AreSame(VolumeXmlEndpoint, IdentityEndpoints.ResolveAnnotationServiceEndpoint(VolumeXmlEndpoint, identity));
        }

        [TestMethod]
        public void ResolveAnnotationServiceEndpoint_NoXmlAndNoIdentity_ReturnsNull()
        {
            Assert.IsNull(IdentityEndpoints.ResolveAnnotationServiceEndpoint(null, " "));
        }

        [TestMethod]
        public void ResolveAnnotationServiceEndpoint_NoXmlButIdentity_UsesIdentity()
        {
            Assert.AreEqual(new Uri("https://example.test/new/Service.svc"),
                IdentityEndpoints.ResolveAnnotationServiceEndpoint(null, " https://example.test/new/Service.svc "));
        }

        [TestMethod]
        public void ResolvePermissionsApiUrl_NullIdentityServer_ReturnsXmlUnchanged()
        {
            var xml = new Uri("https://identity.codepharm.net:5001/");
            Assert.AreSame(xml, IdentityEndpoints.ResolvePermissionsApiUrl(xml, null));
            Assert.IsNull(IdentityEndpoints.ResolvePermissionsApiUrl(null, null));
        }

        [TestMethod]
        public void ResolvePermissionsApiUrl_SameHostDifferentCase_IsRewritten()
        {
            var xml = new Uri("https://IDENTITY.CodePharm.net/");
            Assert.AreEqual(new Uri("https://identity.codepharm.net:6001/"),
                IdentityEndpoints.ResolvePermissionsApiUrl(xml, IdentityServer));
        }

        [TestMethod]
        public void ResolvePermissionsApiUrl_SameHostHttpWebApiPort_Unchanged()
        {
            var xml = new Uri("http://identity.codepharm.net:6000/");
            Assert.AreSame(xml, IdentityEndpoints.ResolvePermissionsApiUrl(xml, IdentityServer));
        }

        [TestMethod]
        public void ResolvePermissionsApiUrl_DifferentHostOnIdentityPort_Unchanged()
        {
            var proxy = new Uri("https://api.example.test:5001/");
            Assert.AreSame(proxy, IdentityEndpoints.ResolvePermissionsApiUrl(proxy, IdentityServer));
        }

        [TestMethod]
        public void ResolvePermissionsApiUrl_RewriteUsesIdentityServerScheme()
        {
            var httpIdentity = new Uri("http://identity.codepharm.net:5000/");
            var xml = new Uri("https://identity.codepharm.net/");
            Assert.AreEqual(new Uri("http://identity.codepharm.net:6000/"),
                IdentityEndpoints.ResolvePermissionsApiUrl(xml, httpIdentity));
        }

        [TestMethod]
        public void FromIdentityServer_StripsPathQueryAndFragment()
        {
            var resolved = IdentityEndpoints.FromIdentityServer(new Uri("https://identity.codepharm.net:5001/connect/token?x=1#frag"));
            Assert.AreEqual("https://identity.codepharm.net:6001/", resolved.AbsoluteUri);
        }

        [TestMethod]
        public void FromIdentityServer_HttpsSchemeIsCaseInsensitive()
        {
            Assert.AreEqual(6001, IdentityEndpoints.FromIdentityServer(new Uri("HTTPS://identity.codepharm.net/")).Port);
        }

        [TestMethod]
        public void FromIdentityServer_Null_Throws()
        {
            var ex = Assert.ThrowsException<ArgumentNullException>(() => IdentityEndpoints.FromIdentityServer(null));
            Assert.AreEqual("identityServerUrl", ex.ParamName);
        }

        [DataTestMethod]
        [DataRow("RC1", "  RC1-Annotate  ", "RC1-Annotate")]
        [DataRow("RC1", "", "RC1")]
        [DataRow("RC1", "   ", "RC1")]
        [DataRow(null, null, "")]
        [DataRow(null, " ", "")]
        public void ResolveScopeResourceName_TrimsServerNameAndIgnoresBlank(string volume, string server, string expected)
        {
            Assert.AreEqual(expected, IdentityEndpoints.ResolveScopeResourceName(volume, server));
        }
    }
}
