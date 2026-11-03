using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Viking.UI.WPF.ViewModels;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the names the volume picker gives recent volumes that were saved by URL only. The view model is built with no
    /// bearer token and an identity URL that is not a URL, so it loads nothing, makes no network call and handles no
    /// credential.
    /// </summary>
    [TestClass]
    public class VolumeSelectionViewModelTests
    {
        private static VolumeSelectionViewModel Offline() => new(null, "not a url");

        private static string NameOfRecentVolume(string url)
        {
            VolumeSelectionViewModel viewModel = Offline();
            viewModel.AddRecentVolume(url, null);
            return viewModel.RecentVolumes.Single().Name;
        }

        [TestMethod]
        [DataRow("http://rogue1.codepharm.net/RPC1/SliceToVolume.VikingXML", "SliceToVolume", DisplayName = "extension in any case")]
        [DataRow("http://host/data/volume.vikingxml", "volume", DisplayName = "the name keeps its last letter")]
        [DataRow("http://host/data/Mosaic.xml", "Mosaic", DisplayName = "any extension is dropped, not only .vikingxml")]
        [DataRow("http://host/data/my.volume.vikingxml", "my.volume", DisplayName = "only the rightmost dot starts the extension")]
        [DataRow("http://host/data/volume.vikingxml/", "volume", DisplayName = "trailing slash")]
        [DataRow("http://host/data/volume", "volume", DisplayName = "no extension")]
        [DataRow("http://host/data/.hidden", ".hidden", DisplayName = "a leading dot is not an extension")]
        [DataRow("http://host/data/a.vikingxml", "a", DisplayName = "a one-character name")]
        public void ARecentVolumeSavedByUrlIsNamedAfterTheFileWithoutItsExtension(string url, string expected)
        {
            Assert.AreEqual(expected, NameOfRecentVolume(url));
        }

        [TestMethod]
        public void AUrlWithoutAPathIsNamedAfterItsHost()
        {
            Assert.AreEqual("example.com", NameOfRecentVolume("http://example.com"));
        }

        [TestMethod]
        public void TextThatIsNotAUrlIsKeptAsTheName()
        {
            Assert.AreEqual("not a url", NameOfRecentVolume("not a url"));
        }

        [TestMethod]
        public void AGivenNameWinsOverTheOneFromTheUrl()
        {
            VolumeSelectionViewModel viewModel = Offline();
            viewModel.AddRecentVolume("http://host/data/volume.vikingxml", "RPC1");

            Assert.AreEqual("RPC1", viewModel.RecentVolumes.Single().Name);
        }

        [TestMethod]
        public void AddingTheSameUrlAgainMovesItToTheTopWithoutDuplicating()
        {
            VolumeSelectionViewModel viewModel = Offline();
            viewModel.AddRecentVolume("http://host/a/first.vikingxml", null);
            viewModel.AddRecentVolume("http://host/b/second.vikingxml", null);
            viewModel.AddRecentVolume("HTTP://HOST/A/FIRST.VIKINGXML", null);

            CollectionAssert.AreEqual(new[] { "FIRST", "second" }, viewModel.RecentVolumes.Select(v => v.Name).ToArray());
        }
    }
}
