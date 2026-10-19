using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the URLs <see cref="Export"/> builds for the Tulip export menu items. Only the protected static
    /// builders are exercised through <see cref="TestExport"/>; the Open* methods call Process.Start and are not run.
    /// </summary>
    [TestClass]
    public class ExportRequestTests
    {
        /// <summary>Exposes the protected static URL builders of <see cref="Export"/>.</summary>
        private sealed class TestExport(Uri url) : Export(url)
        {
            public static Uri Morphology(Uri baseUrl, long id) => BuildMorphologyRequest(baseUrl, id);
            public static Uri Network(Uri baseUrl, long id, long? hops) => BuildNetworkRequest(baseUrl, id, hops);
            public static Uri Motif(Uri baseUrl) => BuildMotifRequest(baseUrl);
        }

        private static readonly Uri SlashBase = new("https://export.example.com/api/");

        [TestMethod]
        public void ConstructorKeepsExportUrl()
        {
            Assert.AreSame(SlashBase, new Export(SlashBase).ExportURL);
        }

        [TestMethod]
        public void MorphologyUsesIdQuery()
        {
            Assert.AreEqual("https://export.example.com/api/morphology/tlp?id=1234", TestExport.Morphology(SlashBase, 1234).ToString());
        }

        [TestMethod]
        public void MorphologyIdIsCultureInvariantForLargeValues()
        {
            Assert.AreEqual("https://export.example.com/api/morphology/tlp?id=9876543210", TestExport.Morphology(SlashBase, 9876543210L).ToString());
        }

        [TestMethod]
        public void NetworkWithoutHopsOmitsHopsParameter()
        {
            Assert.AreEqual("https://export.example.com/api/network/tlp?id=42", TestExport.Network(SlashBase, 42, null).ToString());
        }

        [TestMethod]
        public void NetworkWithHopsAppendsHops()
        {
            Assert.AreEqual("https://export.example.com/api/network/tlp?id=42&hops=3", TestExport.Network(SlashBase, 42, 3).ToString());
        }

        [TestMethod]
        public void NetworkWithZeroHopsStillAppendsHops()
        {
            Assert.AreEqual("https://export.example.com/api/network/tlp?id=42&hops=0", TestExport.Network(SlashBase, 42, 0).ToString());
        }

        [TestMethod]
        public void MotifHasNoQuery()
        {
            Assert.AreEqual("https://export.example.com/api/motifs/tlp", TestExport.Motif(SlashBase).ToString());
        }

        [TestMethod]
        public void BaseWithoutTrailingSlashLosesLastSegment()
        {
            // Uri relative resolution replaces the last path segment; the configured export URL must end in '/'.
            Uri noSlash = new("https://export.example.com/api");
            Assert.AreEqual("https://export.example.com/morphology/tlp?id=7", TestExport.Morphology(noSlash, 7).ToString());
            Assert.AreEqual("https://export.example.com/network/tlp?id=7&hops=2", TestExport.Network(noSlash, 7, 2).ToString());
            Assert.AreEqual("https://export.example.com/motifs/tlp", TestExport.Motif(noSlash).ToString());
        }

        [TestMethod]
        public void HostOnlyBaseNeedsNoTrailingSlash()
        {
            Uri hostOnly = new("https://export.example.com");
            Assert.AreEqual("https://export.example.com/motifs/tlp", TestExport.Motif(hostOnly).ToString());
        }
    }
}
