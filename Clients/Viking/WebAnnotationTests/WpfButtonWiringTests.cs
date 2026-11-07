using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace WebAnnotationTests
{
    /// <summary>
    /// Every button in the WebAnnotationWPFControls windows and controls does something: it has a <c>Click</c> handler,
    /// a <c>Command</c>, or is a dialog's default or cancel button. A button with none of these looks clickable and does
    /// nothing, as Create New Type in the F7 structure type manager once did. The XAML compiler already fails when a
    /// named <c>Click</c> handler is missing, so this only checks that one is named. Known stubs are listed with the
    /// reason they remain; a new dead button fails until it is wired or listed.
    /// </summary>
    [TestClass]
    public class WpfButtonWiringTests
    {
        private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        /// <summary>Attributes that make a button act when clicked.</summary>
        private static readonly string[] ActionAttributes = ["Click", "Command", "IsDefault", "IsCancel"];

        /// <summary>
        /// Folders that hold design-time data only (<c>d:DesignData</c>), not UI, so they are not checked. One of their
        /// files is not well-formed XML and is excluded from the build.
        /// </summary>
        private static readonly string[] SkippedFolders = ["bin", "obj", "SampleData"];

        /// <summary>The dead buttons left as they are, as (file under WebAnnotationWPFControls, button text, why).</summary>
        private static readonly (string File, string Text, string Why)[] KnownStubs =
        [
            ("Controls/StructureTypeTree.xaml", "Add", "unfinished stub, deferred by the improvement loop"),
            ("Forms/SelectStructureTypeForm.xaml", "Done!", "unfinished stub, deferred by the improvement loop"),
        ];

        private static string ControlsDirectory([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "WebAnnotationWPFControls"));

        /// <summary>Every button with no action, as (file relative to the project with '/' separators, button text).</summary>
        private static List<(string File, string Text)> UnwiredButtons()
        {
            string root = ControlsDirectory();
            Assert.IsTrue(Directory.Exists(root), $"{root} not found");

            List<(string File, string Text)> unwired = [];
            foreach (string path in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
            {
                string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
                if (SkippedFolders.Any(folder => relative.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)))
                    continue;

                foreach (XElement button in XDocument.Load(path).Descendants(Presentation + "Button"))
                {
                    if (!ActionAttributes.Any(name => button.Attribute(name) is not null))
                        unwired.Add((relative, button.Value.Trim()));
                }
            }

            return unwired;
        }

        [TestMethod]
        public void EveryButton_HasAnAction_OrIsAKnownStub()
        {
            string[] unexpected = [.. UnwiredButtons()
                .Where(b => !KnownStubs.Any(s => s.File == b.File && s.Text == b.Text))
                .Select(b => $"{b.File}: \"{b.Text}\"")];

            Assert.AreEqual(0, unexpected.Length,
                "Buttons with no Click, Command, IsDefault or IsCancel do nothing when clicked; wire them or list them in KnownStubs:\n" +
                string.Join("\n", unexpected));
        }

        /// <summary>A stub that has since been wired or removed is taken off the list, so the list stays true.</summary>
        [TestMethod]
        public void KnownStubs_AreStillUnwired()
        {
            List<(string File, string Text)> unwired = UnwiredButtons();
            string[] stale = [.. KnownStubs
                .Where(s => !unwired.Contains((s.File, s.Text)))
                .Select(s => $"{s.File}: \"{s.Text}\"")];

            Assert.AreEqual(0, stale.Length, "Remove these from KnownStubs; they are no longer dead buttons:\n" + string.Join("\n", stale));
        }
    }
}
