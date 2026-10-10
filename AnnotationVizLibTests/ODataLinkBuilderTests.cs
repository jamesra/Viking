using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationVizLibTests
{
    [TestClass]
    public class ODataLinkBuilderTests
    {
        [TestMethod]
        public void AnnotationHostBecomesODataRoot()
        {
            string root = ODataLinkBuilder.ServiceRoot(
                "https://websvc.codepharm.net/RC2/Annotation/Service.svc");

            Assert.AreEqual("https://websvc.codepharm.net/RC2/OData", root);
        }

        [TestMethod]
        public void StructureQueryUsesTheODataRoot()
        {
            string url = ODataLinkBuilder.Structure(
                "https://websvc.codepharm.net/RC1/Annotation/Annotate.svc",
                180);

            Assert.AreEqual("https://websvc.codepharm.net/RC1/OData/Structures(180)", url);
        }

        [TestMethod]
        public void ExistingODataRootIsNotDoubled()
        {
            string url = ODataLinkBuilder.Location(
                "https://websvc.codepharm.net/RC1/OData/",
                42);

            Assert.AreEqual("https://websvc.codepharm.net/RC1/OData/Locations(42)", url);
        }

        [TestMethod]
        public void VolumeServiceRootAppendsOData()
        {
            string root = ODataLinkBuilder.ServiceRoot("https://websvc.codepharm.net/RC1");

            Assert.AreEqual("https://websvc.codepharm.net/RC1/OData", root);
        }

        [TestMethod]
        public void ExportRootReplacesAnnotation()
        {
            string root = ODataLinkBuilder.ExportServiceRoot(
                "https://websvc.codepharm.net/RC2/Annotation/Service.svc");

            Assert.AreEqual("https://websvc.codepharm.net/RC2/Export", root);
        }
    }
}
