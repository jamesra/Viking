using System;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="Global.GetEndpointFromXML"/>: a VikingXML without <c>DefaultWebAnnotationUserSettings</c> (RC1 v2's
    /// did not have one) still gives the annotation endpoint, instead of the module failing to start.
    /// </summary>
    [TestClass]
    public class GlobalEndpointFromXmlTests
    {
        [TestInitialize]
        public void Initialize()
        {
            SegmentationServiceSessionTests.EnsureVolumeCachePathAvailable();
            AccessibleVolumeSession.Clear();
        }

        [TestCleanup]
        public void ClearIdentitySession() => AccessibleVolumeSession.Clear();

        [TestMethod]
        public void VolumeToEndpointWithoutUserSettingsElement_SetsTheEndpoint()
        {
            var volume = new XElement(
                "Volume",
                new XElement("VolumeToEndpoint",
                    new XAttribute("Name", "RC1"),
                    new XAttribute("Endpoint", "https://annotation.example/RC1/Annotation/Service.svc")));

            Assert.IsTrue(Global.GetEndpointFromXML(volume));
            Assert.AreEqual(new Uri("https://annotation.example/RC1/Annotation/Service.svc"), WebAnnotationModel.State.Endpoint);
        }

        [TestMethod]
        public void IdentityEndpointWithoutUserSettingsElement_SetsTheEndpoint()
        {
            AccessibleVolumeSession.SetAnnotationEndpointFromIdentity("https://identity-chosen.example/RC1/Annotation/Service.svc");
            var volume = new XElement("Volume", new XElement("Channels"));

            Assert.IsTrue(Global.GetEndpointFromXML(volume));
            Assert.AreEqual(new Uri("https://identity-chosen.example/RC1/Annotation/Service.svc"), WebAnnotationModel.State.Endpoint);
        }
    }
}
