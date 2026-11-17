using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;
using Viking.Common;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Checks the repo copy of the default shortcuts file (<c>Clients/Viking/WebAnnotation/UserSettings.xml</c>,
    /// deployed to the server as <c>WebAnnotationUserSettings.xml</c>) against <see cref="OverlayHotkeys"/>:
    /// a chord bound twice or bound to a key the overlay handles in code is a shortcut that never fires. Also checks that
    /// the reserved chords are exactly the overlay's key table and that the hotkey help says which hotkeys cannot fire.
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

        private static Keys KeyData(Hotkey h) => AnnotationOverlay.ChordOf(h);

        private static string Describe(Hotkey h) => $"{KeyData(h)} ({h.Action})";

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
        [DataRow(Keys.F11, true, DisplayName = "F11 (goto structure) is the overlay's")]
        [DataRow(Keys.Control | Keys.F12, true, DisplayName = "F12 (goto location) is taken with any modifier")]
        [DataRow(Keys.F3, true)]
        [DataRow(Keys.Back, true)]
        [DataRow(Keys.Shift | Keys.ShiftKey, false)]
        public void IsReservedMatchesTheOverlayHandlers(Keys keyData, bool expected)
        {
            Assert.AreEqual(expected, OverlayHotkeys.IsReserved(keyData));
        }

        /// <summary>F6, F7 and Ctrl+Z were handled without help lines; every overlay key now has one.</summary>
        [TestMethod]
        [DataRow(HelpInput.F6, HelpModifiers.None)]
        [DataRow(HelpInput.F7, HelpModifiers.None)]
        [DataRow(HelpInput.Z, HelpModifiers.Ctrl)]
        [DataRow(HelpInput.F11, HelpModifiers.None)]
        [DataRow(HelpInput.F12, HelpModifiers.None)]
        public void TheOverlayTableDocumentsItsKeys(HelpInput input, HelpModifiers modifiers)
        {
            Assert.IsTrue(OverlayHotkeys.Table.Any(b => b.Help.Modifiers == modifiers && b.Help.Inputs!.Contains(input) &&
                                                        b.Help.Description.Length > 0));
        }

        private static Hotkey NewHotkey(string keyName, string action, bool shift = false, bool ctrl = false) =>
            new() { KeyName = keyName, Action = action, Shift = shift, Ctrl = ctrl, Alt = false };

        [TestMethod]
        public void HotkeyHelpMarksTheHotkeysThatCannotFire()
        {
            List<string> reported = [];
            HelpEntry[] help = AnnotationOverlay.BuildUserHotkeyHelp(
            [
                NewHotkey("C", "Circle"),
                NewHotkey("F5", "Shadowed by reload"),
                NewHotkey("C", "Second C"),
                NewHotkey("OemPeriod", "Period", ctrl: true),
            ], null, reported.Add);

            Assert.AreEqual(4, help.Length);
            Assert.AreEqual("Circle", help[0].Description);
            CollectionAssert.AreEqual(new[] { HelpInput.C }, help[0].Inputs);
            StringAssert.StartsWith(help[1].Description, "Shadowed by reload (not available: used by the annotation overlay");
            StringAssert.StartsWith(help[2].Description, "Second C (not available: used by the hotkey for Circle");
            Assert.AreEqual(HelpInput.Other, help[3].Inputs![0]);
            Assert.AreEqual("OemPeriod", help[3].Label);
            Assert.AreEqual(HelpModifiers.Ctrl, help[3].Modifiers);
            Assert.AreEqual(2, reported.Count);
        }

        [TestMethod]
        public void RepoHotkeyHelpHasNoUnavailableHotkey()
        {
            UserSettings settings = UserSettings.Load(FindRepoUserSettings());
            List<string> reported = [];
            HelpEntry[] help = AnnotationOverlay.BuildUserHotkeyHelp(settings.Shortcuts.Hotkey, null, reported.Add);
            Assert.AreEqual(settings.Shortcuts.Hotkey.Count, help.Length);
            Assert.AreEqual(0, reported.Count, string.Join("\n", reported));
        }

        [TestMethod]
        public void HotkeysAreGroupedByActionKindWithoutTheWordsTheirHeadingSays()
        {
            Dictionary<string, AnnotationOverlay.HotkeyActionKind> kinds = new()
            {
                ["Toggle Soma Tag"] = AnnotationOverlay.HotkeyActionKind.StructureTag,
                ["Toggle No Axon"] = AnnotationOverlay.HotkeyActionKind.StructureTag,
                ["Toggle Terminal Tag"] = AnnotationOverlay.HotkeyActionKind.LocationTag,
                ["Annotate Gap Junction"] = AnnotationOverlay.HotkeyActionKind.Annotate,
                ["Convert Annotation To Circle"] = AnnotationOverlay.HotkeyActionKind.ConvertAnnotation,
            };
            HelpEntry[] help = AnnotationOverlay.BuildUserHotkeyHelp(
                [NewHotkey("S", "Toggle Soma Tag", shift: true), NewHotkey("N", "Toggle No Axon"), NewHotkey("X", "Toggle Terminal Tag"),
                 NewHotkey("G", "Annotate Gap Junction"), NewHotkey("D1", "Convert Annotation To Circle"), NewHotkey("C", "Circle")],
                null, null, kinds);

            Assert.IsTrue(help[0].IsToggle);
            Assert.AreEqual(("Soma", "Structure tags"), (help[0].Description, help[0].Group));
            Assert.AreEqual("No Axon", help[1].Description, "a name without the word Tag keeps all its words");
            Assert.AreEqual(("Terminal", "Location tags"), (help[2].Description, help[2].Group));
            Assert.IsTrue(help[2].IsToggle);
            Assert.AreEqual(("Gap Junction", "Annotate"), (help[3].Description, help[3].Group));
            Assert.IsFalse(help[3].IsToggle);
            Assert.AreEqual(("To Circle", "Convert annotation"), (help[4].Description, help[4].Group));
            Assert.AreEqual(("Circle", (string?)null), (help[5].Description, help[5].Group), "an action the settings do not define is listed as named");
        }

        [TestMethod]
        public void RepoToggleActionsAreFoundByName()
        {
            UserSettings settings = UserSettings.Load(FindRepoUserSettings());
            Dictionary<string, AnnotationOverlay.HotkeyActionKind> kinds = AnnotationOverlay.HotkeyActionKinds(settings);
            Assert.AreEqual(AnnotationOverlay.HotkeyActionKind.StructureTag, kinds["Toggle Soma Tag"]);
            Assert.AreEqual(AnnotationOverlay.HotkeyActionKind.LocationTag, kinds["Toggle Questionable Tag"]);
            Assert.AreEqual(AnnotationOverlay.HotkeyActionKind.Annotate, kinds["Annotate Gap Junction"]);
            Assert.AreEqual(AnnotationOverlay.HotkeyActionKind.ConvertAnnotation, kinds["Convert Annotation To Circle"]);

            HelpEntry[] help = AnnotationOverlay.BuildUserHotkeyHelp(settings.Shortcuts.Hotkey, null, null, kinds);
            Assert.IsFalse(help.Any(e => e.Group is null), "every shipped hotkey runs a defined action, so each has a heading");
            Assert.IsFalse(help.Any(e => e.IsToggle && e.Description.StartsWith("Toggle ", System.StringComparison.Ordinal)),
                "every shipped tag is named Toggle …, so the help can drop the word");
        }
    }
}
