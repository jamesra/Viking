using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;
using Viking.UI.Controls;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>The per-section contrast windows kept on this computer, and how they replace a crushed channel's display range.</summary>
    [TestClass]
    public class CrushContrastStoreTests
    {
        private string _path = "";

        [TestInitialize]
        public void MakePath() => _path = Path.Combine(Path.GetTempPath(), "CrushContrastStoreTests-" + Guid.NewGuid().ToString("N"), "CrushContrast.xml");

        [TestCleanup]
        public void DeleteFolder()
        {
            string? folder = Path.GetDirectoryName(_path);
            if (folder is not null && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [TestMethod]
        public void ASavedWindowSurvivesANewStore()
        {
            new CrushContrastStore(_path).Set("RC2", 139, "TEM.Raw8", 5, 40.5);

            (double Min, double Max)? loaded = new CrushContrastStore(_path).Get("RC2", 139, "TEM.Raw8");
            Assert.AreEqual((5.0, 40.5), loaded);
        }

        [TestMethod]
        public void WindowsAreKeyedByVolumeSectionAndChannelIgnoringCase()
        {
            CrushContrastStore store = new(_path);
            store.Set("RC2", 139, "TEM.Raw8", 5, 40);

            Assert.IsNotNull(store.Get("rc2", 139, "tem.raw8"));
            Assert.IsNull(store.Get("RC2", 943, "TEM.Raw8"), "other section");
            Assert.IsNull(store.Get("RC1", 139, "TEM.Raw8"), "other volume");
            Assert.IsNull(store.Get("RC2", 139, "TEM"), "other channel");
        }

        [TestMethod]
        public void ClearReturnsTheSectionToTheVolumeRangeAndIsSaved()
        {
            CrushContrastStore store = new(_path);
            store.Set("RC2", 139, "TEM.Raw8", 5, 40);
            store.Clear("RC2", 139, "TEM.Raw8");

            Assert.IsNull(store.Get("RC2", 139, "TEM.Raw8"));
            Assert.IsNull(new CrushContrastStore(_path).Get("RC2", 139, "TEM.Raw8"));
        }

        [TestMethod]
        public void AWindowWithNoWidthIsIgnored()
        {
            CrushContrastStore store = new(_path);
            store.Set("RC2", 139, "TEM.Raw8", 40, 40);
            store.Set("RC2", 139, "TEM.Raw8", 40, 10);

            Assert.IsNull(store.Get("RC2", 139, "TEM.Raw8"));
            Assert.IsFalse(File.Exists(_path));
        }

        [TestMethod]
        public void AnUnreadableFileMeansNoSavedWindowsAndIsReplacedOnTheNextSet()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, "<not xml");

            CrushContrastStore store = new(_path);
            Assert.IsNull(store.Get("RC2", 139, "TEM.Raw8"));

            store.Set("RC2", 139, "TEM.Raw8", 1, 9);
            Assert.AreEqual((1.0, 9.0), new CrushContrastStore(_path).Get("RC2", 139, "TEM.Raw8"));
        }

        [TestMethod]
        public void AUserWindowReplacesTheDisplayRangeAndStillScalesWithContrast()
        {
            double saved = State.CrushContrast;
            try
            {
                Volume volume = (Volume)FormatterServices.GetUninitializedObject(typeof(Volume));
                Section section = new(volume, "0007", XElement.Parse("<Section number=\"7\" />"));
                section.CrushedChannels["Crushed"] = new CrushInfo(10, 0, 8, (51.0, 102.0), null);

                State.CrushContrast = 1.0;
                (float min, float max) = SectionViewerControl.CrushDisplayWindowFor(section, "Crushed", (25.5, 127.5));
                Assert.AreEqual(0.1, min, 1e-6);
                Assert.AreEqual(0.5, max, 1e-6);

                State.CrushContrast = 2.0;
                (_, max) = SectionViewerControl.CrushDisplayWindowFor(section, "Crushed", (25.5, 127.5));
                Assert.AreEqual(0.3, max, 1e-6);

                (min, _) = SectionViewerControl.CrushDisplayWindowFor(section, "Raw", (25.5, 127.5));
                Assert.AreEqual(0, min, "a channel that is not crushed ignores the user's window");
            }
            finally
            {
                State.CrushContrast = saved;
            }
        }
    }
}
