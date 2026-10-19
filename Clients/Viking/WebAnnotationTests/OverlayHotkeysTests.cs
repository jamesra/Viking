using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Checks the repo copy of the default shortcuts file (<c>Clients/Viking/WebAnnotation/UserSettings.xml</c>,
    /// deployed to the server as <c>WebAnnotationUserSettings.xml</c>) against <see cref="OverlayHotkeys"/>:
    /// a chord bound twice or bound to a key the overlay handles in code is a shortcut that never fires.
    /// </summary>
    [TestClass]
    public class OverlayHotkeysTests
    {
        private const string RepoRelativePath = @"Clients\Viking\WebAnnotation\UserSettings.xml";

        /// <summary>
        /// The repo copy of the settings file: beside this source file's project when the tests run from a
        /// build of this checkout, else found by walking up from the test output folder.
        /// </summary>
        private static string FindRepoUserSettings([CallerFilePath] string thisSourceFile = "")
        {
            string besideSource = Path.Combine(Path.GetDirectoryName(thisSourceFile) ?? "", @"..\WebAnnotation\UserSettings.xml");
            if (File.Exists(besideSource))
                return Path.GetFullPath(besideSource);

            for (DirectoryInfo? dir = new(AppDomain.CurrentDomain.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, RepoRelativePath);
                if (File.Exists(candidate))
                    return candidate;
            }

            Assert.Fail($"{RepoRelativePath} not found beside {thisSourceFile} or above {AppDomain.CurrentDomain.BaseDirectory}");
            return "";
        }

        private static Keys KeyData(Hotkey h) =>
            h.KeyCode | (h.Shift ? Keys.Shift : Keys.None) | (h.Ctrl ? Keys.Control : Keys.None) | (h.Alt ? Keys.Alt : Keys.None);

        private static string Describe(Hotkey h) => $"{h.BuildModifierString()}{h.KeyName} ({h.Action})";

        [TestMethod]
        public void RepoUserSettingsIsWellFormedXml()
        {
            XDocument doc = XDocument.Load(FindRepoUserSettings());
            Assert.AreEqual(XName.Get("UserSettings", "http://rogue1.codepharm.net/XSD/WebAnnotationUserSettings.xsd"), doc.Root!.Name);
        }

        [TestMethod]
        public void EveryRepoHotkeyNamesAReadableKey()
        {
            UserSettings settings = UserSettings.Load(FindRepoUserSettings());
            Assert.IsTrue(settings.Shortcuts.Hotkey.Count > 0);
            string[] unreadable = settings.Shortcuts.Hotkey.Where(h => h.KeyCode == Keys.None).Select(Describe).ToArray();
            Assert.AreEqual(0, unreadable.Length, "Unreadable KeyName: " + string.Join(", ", unreadable));
        }

        [TestMethod]
        public void NoTwoRepoHotkeysShareAChord()
        {
            UserSettings settings = UserSettings.Load(FindRepoUserSettings());
            string[] duplicates = settings.Shortcuts.Hotkey
                .GroupBy(KeyData)
                .Where(g => g.Count() > 1)
                .Select(g => string.Join(" / ", g.Select(Describe)))
                .ToArray();
            Assert.AreEqual(0, duplicates.Length, "Only the first of these fires: " + string.Join("; ", duplicates));
        }

        [TestMethod]
        public void NoRepoHotkeyIsShadowedByAnOverlayKey()
        {
            UserSettings settings = UserSettings.Load(FindRepoUserSettings());
            string[] shadowed = settings.Shortcuts.Hotkey
                .Where(h => OverlayHotkeys.IsReserved(KeyData(h)))
                .Select(Describe)
                .ToArray();
            Assert.AreEqual(0, shadowed.Length, "Handled by AnnotationOverlay before the shortcuts: " + string.Join(", ", shadowed));
        }

        [TestMethod]
        [DataRow(Keys.Shift | Keys.F8, true, DisplayName = "Shift+F8 resubmits auto-polygonize")]
        [DataRow(Keys.F8, false, DisplayName = "F8 is left to the Done tag shortcut")]
        [DataRow(Keys.Control | Keys.F8, false)]
        [DataRow(Keys.F6, true)]
        [DataRow(Keys.Shift | Keys.F6, true, DisplayName = "F6 is taken with any modifier")]
        [DataRow(Keys.Control | Keys.Alt | Keys.F7, true)]
        [DataRow(Keys.Shift | Keys.Tab, true)]
        [DataRow(Keys.Control | Keys.Z, true)]
        [DataRow(Keys.Control | Keys.Shift | Keys.Z, false)]
        [DataRow(Keys.Z, false)]
        [DataRow(Keys.Shift | Keys.D8, true)]
        [DataRow(Keys.D8, false)]
        [DataRow(Keys.Multiply, true)]
        [DataRow(Keys.F11, false, DisplayName = "F11 also lets the shortcuts run")]
        [DataRow(Keys.Shift | Keys.ShiftKey, false)]
        public void IsReservedMatchesTheOverlayHandlers(Keys keyData, bool expected)
        {
            Assert.AreEqual(expected, OverlayHotkeys.IsReserved(keyData));
        }
    }
}
