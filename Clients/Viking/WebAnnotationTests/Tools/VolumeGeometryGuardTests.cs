using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace WebAnnotationTests.Tools
{
    /// <summary>
    /// Views and view models draw and hit-test what <c>LocationVolumeGeometry</c> gives: they never map a point
    /// (<c>SectionToVolume(</c>, <c>TrySectionToVolume(</c>, <c>TryMapShapeSectionToVolume(</c>) or read the stored volume
    /// cache (<c>.VolumePosition</c>, <c>.VolumeShape</c>). See <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
    /// </summary>
    [TestClass]
    public class VolumeGeometryGuardTests
    {
        private static readonly Regex Forbidden = new(@"SectionToVolume\(|\.VolumePosition\b|\.VolumeShape\b", RegexOptions.Compiled);

        /// <summary>
        /// The justified exceptions, as (file under WebAnnotation, text on the line, why). An empty text allows the whole file.
        /// Phase 2 of the redesign moves the edit code here onto the inverse rule and removes its entries.
        /// </summary>
        private static readonly (string File, string Line, string Why)[] AllowList =
        [
            ("ViewModel/Location_ViewModelBase.cs", "TrySectionToVolume(ring", "Edit mask: an edit that saves (phase 2)"),
            ("ViewModel/Location_ViewModelBase.cs", "VolumeX => modelObj.VolumePosition.X", "the VolumeX property column shows the stored cache itself"),
            ("ViewModel/Location_ViewModelBase.cs", "VolumeY => modelObj.VolumePosition.Y", "the VolumeY property column shows the stored cache itself"),
            ("ViewModel/Structure.cs", "VolumePosition", "Structure.Center picks the location nearest the structure's weighted mean for navigation; not drawn"),
        ];

        private static string WebAnnotationDirectory([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "WebAnnotation"));

        [TestMethod]
        public void ViewsAndViewModelsNeverMapOrReadTheStoredVolumeCache()
        {
            string root = WebAnnotationDirectory();
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "View")), "WebAnnotation sources not found at " + root);

            List<string> violations = [];
            foreach (string folder in new[] { "View", "ViewModel" })
            {
                foreach (string path in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = path.Substring(root.Length + 1).Replace('\\', '/');
                    string[] lines = File.ReadAllLines(path);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string code = lines[i].Trim();
                        if (code.StartsWith("//") || code.StartsWith("*") || code.StartsWith("/*") || !Forbidden.IsMatch(code))
                            continue;

                        if (AllowList.Any(a => a.File == relative && code.Contains(a.Line)))
                            continue;

                        violations.Add($"{relative}:{i + 1}: {code}");
                    }
                }
            }

            Assert.AreEqual(0, violations.Count, "map or read the stored cache through LocationVolumeGeometry:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }

        [TestMethod]
        public void EveryAllowListEntryStillMatchesALine()
        {
            string root = WebAnnotationDirectory();
            foreach (var (file, line, why) in AllowList)
            {
                string path = Path.Combine(root, file);
                Assert.IsTrue(File.Exists(path), $"{file} ({why}) no longer exists");
                Assert.IsTrue(File.ReadAllLines(path).Any(l => Forbidden.IsMatch(l) && l.Contains(line)), $"{file}: '{line}' ({why}) is stale; remove it");
            }
        }
    }
}
