using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingTests
{
    [TestClass]
    public class IdentityEndpointsTests
    {
        static readonly Uri IdentityServer = new Uri("https://identity.codepharm.net:5001/");

        [TestMethod]
        public void Resolve_NullXml_UsesIdentityHostPort6001()
        {
            var resolved = IdentityEndpoints.ResolvePermissionsApiUrl(null, IdentityServer);
            Assert.AreEqual("https://identity.codepharm.net:6001/", resolved.ToString());
        }

        [TestMethod]
        public void Resolve_PublicHostWithoutPort_RewritesTo6001()
        {
            var xml = new Uri("https://identity.codepharm.net/");
            var resolved = IdentityEndpoints.ResolvePermissionsApiUrl(xml, IdentityServer);
            Assert.AreEqual("https://identity.codepharm.net:6001/", resolved.ToString());
        }

        [TestMethod]
        public void Resolve_IdentityServerPort_RewritesTo6001()
        {
            var xml = new Uri("https://identity.codepharm.net:5001/");
            var resolved = IdentityEndpoints.ResolvePermissionsApiUrl(xml, IdentityServer);
            Assert.AreEqual("https://identity.codepharm.net:6001/", resolved.ToString());
        }

        [TestMethod]
        public void Resolve_AlreadyWebApiPort_Unchanged()
        {
            var xml = new Uri("https://identity.codepharm.net:6001/");
            var resolved = IdentityEndpoints.ResolvePermissionsApiUrl(xml, IdentityServer);
            Assert.AreEqual(xml, resolved);
        }

        [TestMethod]
        public void Resolve_DifferentHost_Unchanged()
        {
            var xml = new Uri("https://api.example.com/");
            var resolved = IdentityEndpoints.ResolvePermissionsApiUrl(xml, IdentityServer);
            Assert.AreEqual(xml, resolved);
        }

        [TestMethod]
        public void FromIdentityServer_Http_UsesPort6000()
        {
            var resolved = IdentityEndpoints.FromIdentityServer(new Uri("http://localhost:5000/"));
            Assert.AreEqual("http://localhost:6000/", resolved.ToString());
        }

        [TestMethod]
        public void ResolveScopeResourceName_PrefersAnnotationServerName()
        {
            Assert.AreEqual("RC1-Annotate", IdentityEndpoints.ResolveScopeResourceName("Rabbit Retina", "RC1-Annotate"));
        }

        [TestMethod]
        public void ResolveScopeResourceName_FallsBackToVolumeName()
        {
            Assert.AreEqual("RC2", IdentityEndpoints.ResolveScopeResourceName("RC2", null));
        }

        [TestMethod]
        public void ResolveAnnotationServiceEndpoint_PrefersIdentityField()
        {
            var xml = new Uri("https://example.com/old/Annotate.svc");
            var identity = "https://example.com/new/Annotate.svc";
            var resolved = IdentityEndpoints.ResolveAnnotationServiceEndpoint(xml, identity);
            Assert.AreEqual(identity, resolved!.ToString());
        }

        [TestMethod]
        public void ResolveAnnotationServiceEndpoint_FallsBackToXml()
        {
            var xml = new Uri("https://example.com/old/Annotate.svc");
            var resolved = IdentityEndpoints.ResolveAnnotationServiceEndpoint(xml, null);
            Assert.AreEqual(xml, resolved);
        }
    }
}
