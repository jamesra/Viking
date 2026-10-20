using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotation.Tools;
using Keys = System.Windows.Forms.Keys;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins how Jotunn resolves Viking's UserSettings shortcuts, using the repo copy of the default settings file.
    /// </summary>
    [TestClass]
    public class UserSettingsHotkeysTests
    {
        static string RepoUserSettings([CallerFilePath] string thisSourceFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisSourceFile) ?? "", @"..\WebAnnotation\UserSettings.xml"));

        static UserSettingsHotkeys Load()
        {
            string path = RepoUserSettings();
            if (!File.Exists(path))
                Assert.Inconclusive($"{path} is not beside this checkout's sources");
            return UserSettingsHotkeys.Load(path);
        }

        [TestMethod]
        public void PlainKeyCreatesACircleStructure()
        {
            HotkeyCommand command = Load().Find(Keys.C, shift: false, ctrl: false, alt: false);

            Assert.IsNotNull(command);
            Assert.AreEqual(HotkeyCommandKind.CreateStructure, command.Kind);
            Assert.AreEqual(1L, command.TypeId);
            Assert.AreEqual(LocationType.CIRCLE, command.LocationType);
        }

        [TestMethod]
        public void ModifiersMustMatchExactly()
        {
            UserSettingsHotkeys hotkeys = Load();

            HotkeyCommand shifted = hotkeys.Find(Keys.C, shift: true, ctrl: false, alt: false);
            Assert.AreEqual(LocationType.CURVEPOLYGON, shifted.LocationType);

            Assert.IsNull(hotkeys.Find(Keys.C, shift: false, ctrl: false, alt: true));
        }

        [TestMethod]
        public void StructureShortcutCarriesItsTags()
        {
            HotkeyCommand command = Load().Find(Keys.S, shift: false, ctrl: true, alt: false);

            Assert.AreEqual(35L, command.TypeId);
            Assert.AreEqual(LocationType.OPENCURVE, command.LocationType);
            CollectionAssert.AreEqual(new[] { "Conventional" }, command.Tags.ToArray());
        }

        [TestMethod]
        public void TagAndTerminalShortcutsResolve()
        {
            UserSettingsHotkeys hotkeys = Load();

            HotkeyCommand tag = hotkeys.Find(Keys.T, shift: true, ctrl: false, alt: false);
            Assert.AreEqual(HotkeyCommandKind.ToggleStructureTag, tag.Kind);
            Assert.AreEqual("Tyrosine hydroxylase", tag.Tag);

            HotkeyCommand terminal = hotkeys.Find(Keys.X, shift: true, ctrl: false, alt: false);
            Assert.AreEqual(HotkeyCommandKind.ToggleLocationTerminal, terminal.Kind);
        }

        [TestMethod]
        public void WpfKeysMatchTheSameShortcut()
        {
            UserSettingsHotkeys hotkeys = Load();

            Assert.AreEqual("Annotate Gap Junction", hotkeys.Find(System.Windows.Input.Key.G, false, false, false)?.Name);
        }

        [TestMethod]
        public void KeysTheOverlayOwnsNeverMatch()
        {
            UserSettingsHotkeys hotkeys = Load();

            Assert.IsNull(hotkeys.Find(Keys.F5, false, false, false));
            Assert.IsNull(hotkeys.Find(Keys.Z, shift: false, ctrl: true, alt: false));
        }

        [TestMethod]
        public void HelpListsEveryRunnableShortcut()
        {
            UserSettingsHotkeys hotkeys = Load();

            CollectionAssert.Contains(hotkeys.HelpLines.ToList(), "C: Annotate Cell with Circle");
        }

        [TestMethod]
        public void SettingsUriComesFromTheVolumeElement()
        {
            XElement volume = XElement.Parse("<Volume><DefaultWebAnnotationUserSettings Uri=\"https://example.test/settings.xml\"/></Volume>");

            Assert.AreEqual(new Uri("https://example.test/settings.xml"), UserSettingsHotkeys.SettingsUri(volume));
            Assert.IsNull(UserSettingsHotkeys.SettingsUri(new XElement("Volume")));
        }

        [TestMethod]
        public async Task LoadAsyncUsesAFreshCachedCopyWithoutAUri()
        {
            string directory = Path.Combine(Path.GetTempPath(), "UserSettingsHotkeysTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.IsNull(await UserSettingsHotkeys.LoadAsync(new XElement("Volume"), directory));

                string source = RepoUserSettings();
                if (!File.Exists(source))
                    Assert.Inconclusive($"{source} is not beside this checkout's sources");
                File.Copy(source, Path.Combine(directory, "UserSettings.xml"));

                UserSettingsHotkeys loaded = await UserSettingsHotkeys.LoadAsync(new XElement("Volume"), directory);
                Assert.IsNotNull(loaded?.Find(Keys.C, false, false, false));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
