using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="Global.ShouldLoad"/>, which ExtensionManager calls before loading WebAnnotation
    /// when a volume XML includes a usable VolumeToEndpoint mapping.
    /// </summary>
    [TestClass]
    public class GlobalShouldLoadTests
    {
        private sealed class StubExtensionLoadContext : IExtensionLoadContext
        {
            public StubExtensionLoadContext(XElement? volumeElement)
            {
                VolumeElement = volumeElement!;
                VikingXML = volumeElement is null ? new XDocument() : new XDocument(volumeElement);
                VolumeName = "test-volume";
                VolumeHost = "https://example.test/";
            }

            public XDocument VikingXML { get; }

            public XElement VolumeElement { get; }

            public string VolumeName { get; }

            public string VolumeHost { get; }
        }

        [TestInitialize]
        [TestCleanup]
        public void ClearIdentitySession() => AccessibleVolumeSession.Clear();

        [TestMethod]
        public void ShouldLoad_IdentityEndpointWithoutVolumeToEndpoint_ReturnsTrue()
        {
            AccessibleVolumeSession.SetAnnotationEndpointFromIdentity("https://example.test/RC2/Annotation/Service.svc");
            var volume = new XElement("Volume", new XElement("Channels"));
            Assert.IsTrue(Global.ShouldLoad(new StubExtensionLoadContext(volume)));
        }

        [TestMethod]
        public void ShouldLoad_IdentityEndpointAndNullContext_ReturnsTrue()
        {
            AccessibleVolumeSession.SetAnnotationEndpointFromIdentity("https://example.test/RC2/Annotation/Service.svc");
            Assert.IsTrue(Global.ShouldLoad(null!));
        }

        [TestMethod]
        public void ShouldLoad_NullContext_ReturnsFalse()
        {
            Assert.IsFalse(Global.ShouldLoad(null!));
        }

        [TestMethod]
        public void ShouldLoad_NullVolumeElement_ReturnsFalse()
        {
            Assert.IsFalse(Global.ShouldLoad(new StubExtensionLoadContext(null)));
        }

        [TestMethod]
        public void ShouldLoad_NoVolumeToEndpoint_ReturnsFalse()
        {
            var volume = new XElement("Volume", new XElement("Channels"));
            Assert.IsFalse(Global.ShouldLoad(new StubExtensionLoadContext(volume)));
        }

        [TestMethod]
        public void ShouldLoad_MissingEndpointAttribute_ReturnsFalse()
        {
            var volume = new XElement("Volume", new XElement("VolumeToEndpoint"));
            Assert.IsFalse(Global.ShouldLoad(new StubExtensionLoadContext(volume)));
        }

        [TestMethod]
        public void ShouldLoad_EmptyEndpointAttribute_ReturnsFalse()
        {
            var volume = new XElement(
                "Volume",
                new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "   ")));
            Assert.IsFalse(Global.ShouldLoad(new StubExtensionLoadContext(volume)));
        }

        [TestMethod]
        public void ShouldLoad_ValidEndpoint_ReturnsTrue()
        {
            var volume = new XElement(
                "Volume",
                new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "https://annotation.example/")));
            Assert.IsTrue(Global.ShouldLoad(new StubExtensionLoadContext(volume)));
        }

        [TestMethod]
        public void ShouldLoad_UsesFirstVolumeToEndpointWhenSeveralExist()
        {
            var volume = new XElement(
                "Volume",
                new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "https://first.example/")),
                new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "")));
            Assert.IsTrue(Global.ShouldLoad(new StubExtensionLoadContext(volume)));
        }

        [TestMethod]
        public void ShouldLoad_MatchesVolumeToEndpointByLocalNameOnly()
        {
            var volume = new XElement(
                "Volume",
                new XElement("{urn:other}VolumeToEndpoint", new XAttribute("Endpoint", "https://wrong.example/")),
                new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "https://right.example/")));
            Assert.IsTrue(Global.ShouldLoad(new StubExtensionLoadContext(volume)));
        }
    }
}
