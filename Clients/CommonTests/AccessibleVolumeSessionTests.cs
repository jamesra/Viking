using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins the set/clear rules of <see cref="AccessibleVolumeSession"/>. Login applies Identity fields here on every
    /// attempt, so a blank or invalid value must clear the previous login's value: a stale endpoint or server name
    /// would send annotations, exports, or scope checks to the server chosen in an earlier login.
    /// The class is static; each test starts and ends with <see cref="AccessibleVolumeSession.Clear"/>.
    /// </summary>
    [TestClass]
    public class AccessibleVolumeSessionTests
    {
        const string ServiceUrl = "https://example.test/RC2/Annotation/Service.svc";
        const string ExportUrl = "https://example.test/RC2/Export/";
        const string ODataUrl = "https://example.test/RC2/OData/";

        [TestInitialize]
        [TestCleanup]
        public void ResetSession() => AccessibleVolumeSession.Clear();

        static void SetAll()
        {
            AccessibleVolumeSession.SetAnnotationEndpointFromIdentity(ServiceUrl);
            AccessibleVolumeSession.SetAnnotationServerNameFromIdentity("RC2-Annotate");
            AccessibleVolumeSession.SetServiceUrlsFromIdentity(ExportUrl, ODataUrl);
        }

        [TestMethod]
        public void SetAll_ValidValues_AreStoredTrimmedAndParsed()
        {
            AccessibleVolumeSession.SetAnnotationEndpointFromIdentity("  " + ServiceUrl + " ");
            AccessibleVolumeSession.SetAnnotationServerNameFromIdentity("  RC2-Annotate\t");
            AccessibleVolumeSession.SetServiceUrlsFromIdentity(" " + ExportUrl, ODataUrl + " ");

            Assert.AreEqual(new Uri(ServiceUrl), AccessibleVolumeSession.AnnotationServiceEndpoint);
            Assert.AreEqual("RC2-Annotate", AccessibleVolumeSession.AnnotationServerName);
            Assert.AreEqual(new Uri(ExportUrl), AccessibleVolumeSession.ExportEndpoint);
            Assert.AreEqual(new Uri(ODataUrl), AccessibleVolumeSession.ODataEndpoint);
        }

        [TestMethod]
        public void Clear_ResetsEveryField()
        {
            SetAll();
            AccessibleVolumeSession.Clear();

            Assert.IsNull(AccessibleVolumeSession.AnnotationServiceEndpoint);
            Assert.IsNull(AccessibleVolumeSession.AnnotationServerName);
            Assert.IsNull(AccessibleVolumeSession.ExportEndpoint);
            Assert.IsNull(AccessibleVolumeSession.ODataEndpoint);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("/RC2/Annotation/Service.svc")]
        public void SetAnnotationEndpointFromIdentity_UnusableValue_ClearsPreviousEndpoint(string value)
        {
            SetAll();
            AccessibleVolumeSession.SetAnnotationEndpointFromIdentity(value);

            Assert.IsNull(AccessibleVolumeSession.AnnotationServiceEndpoint);
            Assert.AreEqual("RC2-Annotate", AccessibleVolumeSession.AnnotationServerName, "other fields are left alone");
            Assert.AreEqual(new Uri(ExportUrl), AccessibleVolumeSession.ExportEndpoint, "other fields are left alone");
        }

        [TestMethod]
        public void SetAnnotationEndpointFromIdentity_NewValue_ReplacesPrevious()
        {
            SetAll();
            AccessibleVolumeSession.SetAnnotationEndpointFromIdentity("https://other.test/RC1/Annotation/Service.svc");
            Assert.AreEqual(new Uri("https://other.test/RC1/Annotation/Service.svc"), AccessibleVolumeSession.AnnotationServiceEndpoint);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" \t ")]
        public void SetAnnotationServerNameFromIdentity_Blank_ClearsPreviousName(string value)
        {
            SetAll();
            AccessibleVolumeSession.SetAnnotationServerNameFromIdentity(value);

            Assert.IsNull(AccessibleVolumeSession.AnnotationServerName);
            Assert.AreEqual(new Uri(ServiceUrl), AccessibleVolumeSession.AnnotationServiceEndpoint, "other fields are left alone");
        }

        [DataTestMethod]
        [DataRow(null, null)]
        [DataRow("", " ")]
        [DataRow("relative/Export", "/relative/OData")]
        public void SetServiceUrlsFromIdentity_UnusableValues_ClearBothPreviousUrls(string export, string odata)
        {
            SetAll();
            AccessibleVolumeSession.SetServiceUrlsFromIdentity(export, odata);

            Assert.IsNull(AccessibleVolumeSession.ExportEndpoint);
            Assert.IsNull(AccessibleVolumeSession.ODataEndpoint);
            Assert.AreEqual(new Uri(ServiceUrl), AccessibleVolumeSession.AnnotationServiceEndpoint, "other fields are left alone");
        }

        [TestMethod]
        public void SetServiceUrlsFromIdentity_EachUrlIsIndependent()
        {
            AccessibleVolumeSession.SetServiceUrlsFromIdentity(ExportUrl, null);
            Assert.AreEqual(new Uri(ExportUrl), AccessibleVolumeSession.ExportEndpoint);
            Assert.IsNull(AccessibleVolumeSession.ODataEndpoint);

            AccessibleVolumeSession.SetServiceUrlsFromIdentity(null, ODataUrl);
            Assert.IsNull(AccessibleVolumeSession.ExportEndpoint);
            Assert.AreEqual(new Uri(ODataUrl), AccessibleVolumeSession.ODataEndpoint);
        }
    }
}
