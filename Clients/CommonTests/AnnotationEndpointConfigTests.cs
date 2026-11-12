using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Xml.Linq;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins when a volume counts as having an annotation database (<see cref="AnnotationEndpointConfig.IsConfigured"/>): the
    /// rule the WebAnnotation extension loads by, and by which the viewer hides the section number overlay on a plain image stack.
    /// </summary>
    [TestClass]
    public class AnnotationEndpointConfigTests
    {
        [TestInitialize]
        [TestCleanup]
        public void ResetSession() => AccessibleVolumeSession.Clear();

        private static XElement Volume(params object[] children) => new("Volume", children);

        [TestMethod]
        public void NoVolumeOrNoEndpointIsNoDatabase()
        {
            Assert.IsFalse(AnnotationEndpointConfig.IsConfigured(null));
            Assert.IsFalse(AnnotationEndpointConfig.IsConfigured(Volume()));
            Assert.IsFalse(AnnotationEndpointConfig.IsConfigured(Volume(new XElement("VolumeToEndpoint"))));
            Assert.IsFalse(AnnotationEndpointConfig.IsConfigured(Volume(new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "  ")))));
        }

        [TestMethod]
        public void AnEndpointInTheVolumeXmlIsADatabase()
        {
            Assert.IsTrue(AnnotationEndpointConfig.IsConfigured(Volume(new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "https://annotation.example/")))));
        }

        [TestMethod]
        public void OnlyTheFirstMappingCounts()
        {
            Assert.IsFalse(AnnotationEndpointConfig.IsConfigured(Volume(
                new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "")),
                new XElement("VolumeToEndpoint", new XAttribute("Endpoint", "https://second.example/")))));
        }

        [TestMethod]
        public void AnIdentityEndpointIsADatabaseWithoutTheXml()
        {
            AccessibleVolumeSession.AnnotationServiceEndpoint = new Uri("https://example.test/RC1/Annotation/Service.svc");
            Assert.IsTrue(AnnotationEndpointConfig.IsConfigured(null));
        }
    }
}
