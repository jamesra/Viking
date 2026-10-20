using System;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Jotunn gets its export links from <see cref="AnnotationBootstrap"/>, as Viking gets them from its module init:
    /// Identity's export endpoint, else the VikingXML ExportURL attribute.
    /// </summary>
    [TestClass]
    public class AnnotationBootstrapExportTests
    {
        Uri _savedIdentityExport;
        Export _savedExport;
        Uri _savedEndpoint;

        [TestInitialize]
        public void Save()
        {
            _savedIdentityExport = AccessibleVolumeSession.ExportEndpoint;
            _savedExport = WebAnnotation.Global.Export;
            _savedEndpoint = WebAnnotationModel.State.Endpoint;
            AccessibleVolumeSession.ExportEndpoint = null;
            WebAnnotation.Global.Export = null;
        }

        [TestCleanup]
        public void Restore()
        {
            AccessibleVolumeSession.ExportEndpoint = _savedIdentityExport;
            WebAnnotation.Global.Export = _savedExport;
            if (_savedEndpoint != null) // the setter does not take null
                WebAnnotationModel.State.Endpoint = _savedEndpoint;
        }

        static XElement Volume(string mappingAttributes) =>
            XElement.Parse($"<Volume><VolumeToEndpoint Name=\"Test\" Endpoint=\"https://annotation.test/\" {mappingAttributes}/></Volume>");

        [TestMethod]
        public void ExportUrlComesFromTheVolumeXml()
        {
            AnnotationBootstrap.TryPopulateEndpointFromVolumeXml(Volume("ExportURL=\"https://export.test/\""));

            Assert.AreEqual(new Uri("https://export.test/"), WebAnnotation.Global.Export?.ExportURL);
        }

        [TestMethod]
        public void IdentitysExportEndpointWins()
        {
            AccessibleVolumeSession.ExportEndpoint = new Uri("https://identity-export.test/");

            AnnotationBootstrap.TryPopulateEndpointFromVolumeXml(Volume("ExportURL=\"https://export.test/\""));

            Assert.AreEqual(new Uri("https://identity-export.test/"), WebAnnotation.Global.Export?.ExportURL);
        }

        [TestMethod]
        public void NoExportUrlLeavesExportOff()
        {
            AnnotationBootstrap.TryPopulateEndpointFromVolumeXml(Volume(""));

            Assert.IsNull(WebAnnotation.Global.Export);
        }
    }
}
